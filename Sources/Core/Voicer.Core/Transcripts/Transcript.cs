using Voicer.Core.Enums;

namespace Voicer.Core.Transcripts;

/// <summary>
/// A text with the moments of its pieces in a recording, and how it was made: the result of a recognition, or the text
/// of a generated speech.
/// </summary>
/// <param name="SourcePath">The file whose sound was recognized, or the generated sound.</param>
/// <param name="ModelName">The name of the model that recognized or spoke it.</param>
/// <param name="DeviceName">The name of the device the model ran on.</param>
/// <param name="Language">The language of the speech, as a code (<c>en</c>, <c>ru</c>), or <see langword="null"/>.</param>
/// <param name="CreatedAt">When the recognition or the generation finished.</param>
/// <param name="Segments">The pieces of speech, in the order of the recording.</param>
/// <param name="Kind">Whether the speech was recognized or generated.</param>
public sealed record Transcript(
    string SourcePath,
    string ModelName,
    string DeviceName,
    string? Language,
    DateTimeOffset CreatedAt,
    IReadOnlyList<TranscriptSegment> Segments,
    RecordKind Kind = RecordKind.Transcription)
{
    /// <summary>
    /// Gets the recognized text: a line per piece of speech, without empty pieces.
    /// </summary>
    public string Text => string.Join(
        Environment.NewLine,
        Segments.Select(s => s.Text.Trim()).Where(t => t.Length > 0));

    /// <summary>
    /// Gets the name of the recognized file without its folder.
    /// </summary>
    public string SourceName => Path.GetFileName(SourcePath);

    /// <summary>
    /// Gets how long the recognized speech lasts: the end of its last fragment, or zero without speech.
    /// </summary>
    public TimeSpan Duration => Segments.Count == 0 ? TimeSpan.Zero : Segments.Max(s => s.End);
}
