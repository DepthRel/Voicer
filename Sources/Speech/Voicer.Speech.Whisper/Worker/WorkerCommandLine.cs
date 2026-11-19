using System.Globalization;
using Voicer.Core.Devices;
using Voicer.Core.Protocol;

namespace Voicer.Speech.Whisper.Worker;

/// <summary>
/// The command line of the worker process: <c>devices</c>, or
/// <c>transcribe --model &lt;file&gt; --audio &lt;file&gt; --device &lt;index&gt; [--vad &lt;file&gt;]</c>.
/// </summary>
/// <param name="Command">The command, <see cref="WorkerProtocol.DEVICES_COMMAND"/> or <see cref="WorkerProtocol.TRANSCRIBE_COMMAND"/>.</param>
/// <param name="ModelPath">The file of the model, for a recognition.</param>
/// <param name="AudioPath">The WAV file, for a recognition.</param>
/// <param name="DeviceIndex">The index of the video card, or <c>-1</c> for the processor.</param>
/// <param name="DetectorPath">The file of the detector of speech, or <see langword="null"/> to recognize the whole sound.</param>
public sealed record WorkerCommandLine(string Command, string? ModelPath, string? AudioPath, int DeviceIndex, string? DetectorPath = null)
{
    /// <summary>
    /// Reads the command line of the worker.
    /// </summary>
    /// <param name="args">The arguments after <see cref="WorkerProtocol.WORKER_SWITCH"/>.</param>
    /// <param name="commandLine">The command line, when it is valid.</param>
    /// <param name="problem">What is wrong with it, in English, when it is not.</param>
    /// <returns><see langword="true"/> for a valid command line.</returns>
    public static bool TryParse(IReadOnlyList<string> args, out WorkerCommandLine? commandLine, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(args);
        commandLine = null;
        problem = null;

        if (args.Count == 0)
        {
            problem = "A command is expected: devices or transcribe.";

            return false;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2)
        {
            if (i + 1 >= args.Count || !args[i].StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"The argument '{args[i]}' is not an option with a value.";

                return false;
            }

            options[args[i]] = args[i + 1];
        }

        switch (args[0])
        {
            case WorkerProtocol.DEVICES_COMMAND:
                commandLine = new WorkerCommandLine(WorkerProtocol.DEVICES_COMMAND, null, null, ComputeDevice.CPU_INDEX);

                return true;

            case WorkerProtocol.TRANSCRIBE_COMMAND:
                return TryParseTranscribe(options, out commandLine, out problem);

            default:
                problem = $"The command '{args[0]}' is not known.";

                return false;
        }
    }

    /// <summary>
    /// Reads the options of a recognition.
    /// </summary>
    /// <param name="options">The options by name.</param>
    /// <param name="commandLine">The command line, when it is valid.</param>
    /// <param name="problem">What is wrong with it, when it is not.</param>
    /// <returns><see langword="true"/> for valid options.</returns>
    private static bool TryParseTranscribe(IReadOnlyDictionary<string, string> options, out WorkerCommandLine? commandLine, out string? problem)
    {
        commandLine = null;
        problem = null;

        if (!options.TryGetValue(WorkerProtocol.MODEL_OPTION, out var model) || string.IsNullOrWhiteSpace(model) ||
            !options.TryGetValue(WorkerProtocol.AUDIO_OPTION, out var audio) || string.IsNullOrWhiteSpace(audio))
        {
            problem = "A recognition needs --model and --audio.";

            return false;
        }

        var device = ComputeDevice.CPU_INDEX;
        if (options.TryGetValue(WorkerProtocol.DEVICE_OPTION, out var value) &&
            (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out device) || device < ComputeDevice.CPU_INDEX))
        {
            problem = $"The device '{value}' is not an index of a video card or -1.";

            return false;
        }

        options.TryGetValue(WorkerProtocol.DETECTOR_OPTION, out var detector);
        commandLine = new WorkerCommandLine(WorkerProtocol.TRANSCRIBE_COMMAND, model, audio, device, string.IsNullOrWhiteSpace(detector) ? null : detector);

        return true;
    }
}
