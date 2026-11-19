namespace Voicer.Core.Enums;

/// <summary>
/// Why the worker process failed. The code travels instead of a text: the application words it in the language of the
/// user.
/// </summary>
public enum WorkerErrorCode
{
    /// <summary>
    /// The command line of the worker is not valid.
    /// </summary>
    InvalidArguments,

    /// <summary>
    /// The file of the model does not exist.
    /// </summary>
    ModelNotFound,

    /// <summary>
    /// The audio file does not exist.
    /// </summary>
    AudioNotFound,

    /// <summary>
    /// The speech engine could not load the model on the chosen device.
    /// </summary>
    ModelLoadFailed,

    /// <summary>
    /// The recognition stopped with an error.
    /// </summary>
    TranscriptionFailed,

    /// <summary>
    /// The compute devices could not be listed.
    /// </summary>
    DeviceProbeFailed,
}
