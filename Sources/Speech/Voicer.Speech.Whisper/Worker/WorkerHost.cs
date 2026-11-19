using System.Diagnostics;
using System.Text;
using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;
using Voicer.Speech.Whisper.Devices;
using Voicer.Speech.Whisper.Engine;

namespace Voicer.Speech.Whisper.Worker;

/// <summary>
/// The worker process: runs one command of <see cref="WorkerProtocol"/> and writes what happens as JSON lines. A native
/// failure of the speech engine (a driver that crashes) ends this process only; the application sees the exit and
/// tells the user.
/// </summary>
public sealed class WorkerHost
{
    /// <summary>
    /// The exit code of a command that succeeded.
    /// </summary>
    public const int EXIT_OK = 0;

    /// <summary>
    /// The exit code of a command that failed; the last line names the failure.
    /// </summary>
    public const int EXIT_FAILED = 1;

    /// <summary>
    /// The exit code of a command line that is not valid.
    /// </summary>
    public const int EXIT_INVALID_ARGUMENTS = 2;

    /// <summary>
    /// The exit code of a recognition that was cancelled.
    /// </summary>
    public const int EXIT_CANCELLED = 3;

    /// <summary>
    /// The prober of the compute devices.
    /// </summary>
    private readonly IDeviceProbe _probe;

    /// <summary>
    /// The speech engine.
    /// </summary>
    private readonly ISpeechEngine _engine;

    /// <summary>
    /// Where the JSON lines go.
    /// </summary>
    private readonly TextWriter _output;

    /// <summary>
    /// Keeps the lines whole when the engine reports from several threads.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerHost"/> class.
    /// </summary>
    /// <param name="probe">The prober of the compute devices.</param>
    /// <param name="engine">The speech engine.</param>
    /// <param name="output">Where the JSON lines go.</param>
    public WorkerHost(IDeviceProbe probe, ISpeechEngine engine, TextWriter output)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Runs the worker on the console: the JSON lines go to the standard output in UTF-8. With
    /// <see cref="WorkerProtocol.PARENT_OPTION"/> first, the end of the application cancels the work, so no worker
    /// outlives it.
    /// </summary>
    /// <param name="args">The arguments after <see cref="WorkerProtocol.WORKER_SWITCH"/>.</param>
    /// <returns>The exit code.</returns>
    public static async Task<int> RunConsoleAsync(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        await using var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        using var cancellation = new CancellationTokenSource();

        var command = args;
        if (args.Count >= 2 && args[0] == WorkerProtocol.PARENT_OPTION)
        {
            command = [.. args.Skip(2)];
            if (int.TryParse(args[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parent))
            {
                WatchParent(parent, cancellation);
            }
        }

        var host = new WorkerHost(new GgmlDeviceProbe(), new WhisperSpeechEngine(), output);

        return await host.RunAsync(command, cancellation.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a command.
    /// </summary>
    /// <param name="args">The arguments after <see cref="WorkerProtocol.WORKER_SWITCH"/>.</param>
    /// <param name="cancellationToken">Cancels a recognition.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!WorkerCommandLine.TryParse(args, out var commandLine, out var problem))
        {
            WriteError(WorkerErrorCode.InvalidArguments, problem);

            return EXIT_INVALID_ARGUMENTS;
        }

        return commandLine!.Command == WorkerProtocol.DEVICES_COMMAND
            ? ListDevices()
            : await TranscribeAsync(commandLine, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the work when the application that started the worker ends.
    /// </summary>
    /// <param name="parentId">The process id of the application.</param>
    /// <param name="cancellation">The cancellation of the work.</param>
    private static void WatchParent(int parentId, CancellationTokenSource cancellation)
    {
        Process parent;
        try
        {
            parent = Process.GetProcessById(parentId);
        }
        catch (ArgumentException)
        {
            // The application is already gone.
            cancellation.Cancel();

            return;
        }

        _ = Task.Run(
            async () =>
            {
                using (parent)
                {
                    await parent.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }

                await cancellation.CancelAsync().ConfigureAwait(false);
            },
            CancellationToken.None);
    }

    /// <summary>
    /// Lists the compute devices.
    /// </summary>
    /// <returns>The exit code.</returns>
    private int ListDevices()
    {
        try
        {
            Write(new WorkerEvent(WorkerEventKind.Devices) { Devices = _probe.Probe() });

            return EXIT_OK;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WriteError(WorkerErrorCode.DeviceProbeFailed, ex.Message);

            return EXIT_FAILED;
        }
    }

    /// <summary>
    /// Recognizes the speech of a WAV file.
    /// </summary>
    /// <param name="commandLine">The command line of the recognition.</param>
    /// <param name="cancellationToken">Cancels the recognition.</param>
    /// <returns>The exit code.</returns>
    private async Task<int> TranscribeAsync(WorkerCommandLine commandLine, CancellationToken cancellationToken)
    {
        if (!File.Exists(commandLine.ModelPath))
        {
            WriteError(WorkerErrorCode.ModelNotFound, commandLine.ModelPath);

            return EXIT_FAILED;
        }

        if (!File.Exists(commandLine.AudioPath))
        {
            WriteError(WorkerErrorCode.AudioNotFound, commandLine.AudioPath);

            return EXIT_FAILED;
        }

        if (commandLine.DetectorPath is { } detector && !File.Exists(detector))
        {
            WriteError(WorkerErrorCode.ModelNotFound, detector);

            return EXIT_FAILED;
        }

        try
        {
            var language = await _engine.TranscribeAsync(
                commandLine.ModelPath!,
                commandLine.AudioPath!,
                commandLine.DeviceIndex,
                commandLine.DetectorPath,
                new Listener(this),
                cancellationToken).ConfigureAwait(false);
            Write(new WorkerEvent(WorkerEventKind.Done) { Language = language });

            return EXIT_OK;
        }
        catch (OperationCanceledException)
        {
            return EXIT_CANCELLED;
        }
        catch (ModelLoadException ex)
        {
            WriteError(WorkerErrorCode.ModelLoadFailed, ex.Message);

            return EXIT_FAILED;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            WriteError(WorkerErrorCode.TranscriptionFailed, ex.Message);

            return EXIT_FAILED;
        }
    }

    /// <summary>
    /// Writes the line of a failure.
    /// </summary>
    /// <param name="code">Why the work failed.</param>
    /// <param name="detail">The technical detail, in English.</param>
    private void WriteError(WorkerErrorCode code, string? detail)
    {
        Write(new WorkerEvent(WorkerEventKind.Error) { Error = code, Detail = detail });
    }

    /// <summary>
    /// Writes an event as a line.
    /// </summary>
    /// <param name="workerEvent">The event.</param>
    private void Write(WorkerEvent workerEvent)
    {
        var line = WorkerProtocol.Serialize(workerEvent);
        lock (_gate)
        {
            _output.WriteLine(line);
            _output.Flush();
        }
    }

    /// <summary>
    /// Turns what the engine reports into lines.
    /// </summary>
    /// <param name="host">The host that writes the lines.</param>
    private sealed class Listener(WorkerHost host) : ITranscriptionListener
    {
        /// <inheritdoc />
        public void ModelLoaded()
        {
            host.Write(new WorkerEvent(WorkerEventKind.ModelLoaded));
        }

        /// <inheritdoc />
        public void Progress(double percent)
        {
            host.Write(new WorkerEvent(WorkerEventKind.Progress) { Percent = Math.Clamp(percent, 0, 100) });
        }

        /// <inheritdoc />
        public void Segment(TranscriptSegment segment)
        {
            host.Write(new WorkerEvent(WorkerEventKind.Segment) { Segment = segment });
        }
    }
}
