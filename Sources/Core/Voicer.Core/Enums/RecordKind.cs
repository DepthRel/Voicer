namespace Voicer.Core.Enums;

/// <summary>
/// How a record of the history was made. The values are kept in the database: they never change.
/// </summary>
public enum RecordKind
{
    /// <summary>
    /// The speech of a file turned into text.
    /// </summary>
    Transcription = 0,

    /// <summary>
    /// A text turned into speech.
    /// </summary>
    Generation = 1,
}
