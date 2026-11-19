namespace Voicer.UI.Enums;

/// <summary>
/// What a generation of speech is doing.
/// </summary>
public enum GenerationStage
{
    /// <summary>
    /// The sample of the voice is being prepared.
    /// </summary>
    PreparingVoice,

    /// <summary>
    /// The pieces of the text are being spoken, one by one.
    /// </summary>
    Speaking,

    /// <summary>
    /// The spoken pieces are being joined into one recording.
    /// </summary>
    Joining,

    /// <summary>
    /// The recording is being kept in the history.
    /// </summary>
    SavingRecord,
}
