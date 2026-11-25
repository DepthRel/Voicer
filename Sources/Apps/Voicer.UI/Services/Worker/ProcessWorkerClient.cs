using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// <see cref="IWorkerClient"/> that starts a worker process for every command, without a shell and without a window.
/// Cancelling a command ends its process at once: the worker holds nothing that must be saved.
/// </summary>
public sealed class ProcessWorkerClient : IWorkerClient
{
    /// <summary>
    /// How the worker is started.
    /// </summary>
    private readonly WorkerLaunch _launch;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessWorkerClient"/> class.
    /// </summary>
    /// <param name="launch">How the worker is started.</param>
    public ProcessWorkerClient(WorkerLaunch launch)
    {
        _launch = launch ?? throw new ArgumentNullException(nameof(launch));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ComputeDevice>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ComputeDevice>? devices = null;
        await RunAsync(
            [WorkerProtocol.DEVICES_COMMAND],
            e =>
            {
                if (e.Kind == WorkerEventKind.Devices)
                {
                    devices = e.Devices;
                }
            },
            cancellationToken).ConfigureAwait(false);

        return devices ?? throw LocalizedError.Create(m => new WorkerException(m, WorkerErrorCode.DeviceProbeFailed, 0), "Worker.NoAnswer");
    }

    /// <inheritdoc />
    public async Task<WorkerTranscription> TranscribeAsync(
        string modelPath,
        string audioPath,
        int deviceIndex,
        string? detectorPath,
        IProgress<WorkerEvent>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);

        var segments = new List<TranscriptSegment>();
        string? language = null;
        string[] command =
        [
            WorkerProtocol.TRANSCRIBE_COMMAND,
            WorkerProtocol.MODEL_OPTION, Path.GetFullPath(modelPath),
            WorkerProtocol.AUDIO_OPTION, Path.GetFullPath(audioPath),
            WorkerProtocol.DEVICE_OPTION, deviceIndex.ToString(CultureInfo.InvariantCulture),
        ];
        if (detectorPath is not null)
        {
            command = [.. command, WorkerProtocol.DETECTOR_OPTION, Path.GetFullPath(detectorPath)];
        }

        await RunAsync(
            command,
            e =>
            {
                switch (e.Kind)
                {
                    case WorkerEventKind.Segment when e.Segment is not null:
                        segments.Add(e.Segment);
                        break;
                    case WorkerEventKind.Done:
                        language = e.Language;
                        break;
                    default:
                        progress?.Report(e);
                        break;
                }
            },
            cancellationToken).ConfigureAwait(false);

        return new WorkerTranscription(segments, language);
    }

    /// <summary>
    /// Runs one command in a new worker process and waits for its end.
    /// </summary>
    /// <param name="command">The command and its options.</param>
    /// <param name="onEvent">Receives the events of the worker.</param>
    /// <param name="cancellationToken">Ends the worker.</param>
    /// <returns>A task that completes when the worker ended successfully.</returns>
    private async Task RunAsync(IReadOnlyList<string> command, Action<WorkerEvent> onEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var start = new ProcessStartInfo(_launch.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in _launch.ArgumentsFor(command))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        process.Start();

        var session = new WorkerSession(onEvent);
        string? lastLogLine = null;
        var logTask = Task.Run(
            async () =>
            {
                while (await process.StandardError.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lastLogLine = line;
                    }
                }
            },
            CancellationToken.None);

        using (cancellationToken.Register(() => Stop(process)))
        {
            while (await process.StandardOutput.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
            {
                session.HandleLine(line);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await logTask.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        session.Complete(process.ExitCode, lastLogLine);
    }

    /// <summary>
    /// Ends a worker that is no longer needed.
    /// </summary>
    /// <param name="process">The worker process.</param>
    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // The worker ended on its own meanwhile.
        }
    }
}
