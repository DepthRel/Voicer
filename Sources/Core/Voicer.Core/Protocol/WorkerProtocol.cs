using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voicer.Core.Protocol;

/// <summary>
/// The protocol between the application and its worker process: the command line of the worker and one JSON object per
/// line of its standard output. The worker runs the speech engine away from the window, so a failure of a native driver
/// ends the worker, not the application.
/// </summary>
public static class WorkerProtocol
{
    /// <summary>
    /// The first argument that starts the program as a worker instead of the window.
    /// </summary>
    public const string WORKER_SWITCH = "--worker";

    /// <summary>
    /// The option that comes right after <see cref="WORKER_SWITCH"/> with the process id of the application: the worker
    /// stops when that process ends.
    /// </summary>
    public const string PARENT_OPTION = "--parent";

    /// <summary>
    /// The command that lists the compute devices.
    /// </summary>
    public const string DEVICES_COMMAND = "devices";

    /// <summary>
    /// The command that recognizes the speech of a WAV file.
    /// </summary>
    public const string TRANSCRIBE_COMMAND = "transcribe";

    /// <summary>
    /// The option with the path of the model file.
    /// </summary>
    public const string MODEL_OPTION = "--model";

    /// <summary>
    /// The option with the path of the WAV file (16 kHz, mono, 16-bit).
    /// </summary>
    public const string AUDIO_OPTION = "--audio";

    /// <summary>
    /// The option with the index of the video card, or <c>-1</c> for the processor.
    /// </summary>
    public const string DEVICE_OPTION = "--device";

    /// <summary>
    /// The options of the JSON lines: compact, names in camel case, enumerations by name.
    /// </summary>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Writes an event as one line of JSON.
    /// </summary>
    /// <param name="workerEvent">The event.</param>
    /// <returns>The line, without the line end.</returns>
    public static string Serialize(WorkerEvent workerEvent)
    {
        ArgumentNullException.ThrowIfNull(workerEvent);

        return JsonSerializer.Serialize(workerEvent, _json);
    }

    /// <summary>
    /// Reads an event from a line of the output of the worker.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="workerEvent">The event, when the line is one.</param>
    /// <returns>
    /// <see langword="true"/> when the line is an event; anything else (a message a native library printed by itself) is
    /// not.
    /// </returns>
    public static bool TryParse(string? line, out WorkerEvent? workerEvent)
    {
        workerEvent = null;
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith('{'))
        {
            return false;
        }

        try
        {
            workerEvent = JsonSerializer.Deserialize<WorkerEvent>(line, _json);

            return workerEvent is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
