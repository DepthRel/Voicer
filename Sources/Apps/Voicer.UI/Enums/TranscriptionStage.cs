namespace Voicer.UI.Enums;

/// <summary>
/// What a recognition is doing.
/// </summary>
public enum TranscriptionStage
{
    /// <summary>
    /// The sound track is being extracted from the file.
    /// </summary>
    ExtractingAudio,

    /// <summary>
    /// The model is being loaded onto the device.
    /// </summary>
    LoadingModel,

    /// <summary>
    /// The speech is being recognized.
    /// </summary>
    Recognizing,

    /// <summary>
    /// Keeping the text and its compressed sound in the history.
    /// </summary>
    SavingRecord,

    /// <summary>
    /// Writing the summary of the recognized text with a language model.
    /// </summary>
    Summarizing,
}
