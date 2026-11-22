namespace Voicer.Core.Search;

/// <summary>
/// A text with what is marked in it: the words of the query and, in the text of a whole record, the found passage, the
/// phrase being said and the corrected places.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Words">The words that match the query, in their order, not overlapping.</param>
/// <param name="Passage">The found passage, or <see langword="null"/> when the text is the passage itself.</param>
/// <param name="Phrase">The phrase being said as the sound plays, or <see langword="null"/>.</param>
/// <param name="Corrections">The places the user corrected, in their order, or <see langword="null"/> for none.</param>
public sealed record MarkedText(
    string Text,
    IReadOnlyList<TextMark> Words,
    TextMark? Passage = null,
    TextMark? Phrase = null,
    IReadOnlyList<TextMark>? Corrections = null)
{
    /// <summary>
    /// Gets a value indicating whether anything in the text is marked.
    /// </summary>
    public bool HasMarks => Words.Count > 0 || Passage is not null || Phrase is not null || Corrections is { Count: > 0 };
}
