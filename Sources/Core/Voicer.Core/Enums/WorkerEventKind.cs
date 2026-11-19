namespace Voicer.Core.Enums;

/// <summary>
/// What a line of the worker process reports.
/// </summary>
public enum WorkerEventKind
{
    /// <summary>
    /// The compute devices the speech engine can use.
    /// </summary>
    Devices,

    /// <summary>
    /// The model is loaded and recognition starts.
    /// </summary>
    ModelLoaded,

    /// <summary>
    /// How much of the audio is recognized, in percent.
    /// </summary>
    Progress,

    /// <summary>
    /// A recognized piece of speech.
    /// </summary>
    Segment,

    /// <summary>
    /// The work is finished.
    /// </summary>
    Done,

    /// <summary>
    /// The work failed; the event carries the code of the failure.
    /// </summary>
    Error,
}
