using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Localization;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// Follows one run of the worker: reads its lines, hands the events on and decides at the end whether the run failed.
/// </summary>
public sealed class WorkerSession
{
    /// <summary>
    /// Receives every event of the worker except failures.
    /// </summary>
    private readonly Action<WorkerEvent> _onEvent;

    /// <summary>
    /// The failure the worker named, or <see langword="null"/>.
    /// </summary>
    private WorkerEvent? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerSession"/> class.
    /// </summary>
    /// <param name="onEvent">Receives every event of the worker except failures.</param>
    public WorkerSession(Action<WorkerEvent> onEvent)
    {
        _onEvent = onEvent ?? throw new ArgumentNullException(nameof(onEvent));
    }

    /// <summary>
    /// Reads a line of the standard output of the worker. A line that is not an event (a native library that printed
    /// something by itself) is skipped.
    /// </summary>
    /// <param name="line">The line.</param>
    public void HandleLine(string? line)
    {
        if (!WorkerProtocol.TryParse(line, out var workerEvent))
        {
            return;
        }

        if (workerEvent!.Kind == WorkerEventKind.Error)
        {
            _error = workerEvent;

            return;
        }

        _onEvent(workerEvent);
    }

    /// <summary>
    /// Decides how the run ended once the worker exited.
    /// </summary>
    /// <param name="exitCode">The exit code of the worker.</param>
    /// <param name="lastLogLine">The last line the worker wrote to its standard error, or <see langword="null"/>.</param>
    /// <exception cref="WorkerException">The worker named a failure or ended with an error code.</exception>
    public void Complete(int exitCode, string? lastLogLine)
    {
        if (_error is not null)
        {
            var code = _error.Error ?? WorkerErrorCode.TranscriptionFailed;

            throw LocalizedError.Create(
                m => new WorkerException(m, code, exitCode),
                "Worker.Error." + code,
                _error.Detail ?? string.Empty);
        }

        if (exitCode != 0)
        {
            throw LocalizedError.Create(
                m => new WorkerException(m, null, exitCode),
                "Worker.Crashed",
                exitCode,
                lastLogLine ?? string.Empty);
        }
    }
}
