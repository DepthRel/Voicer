namespace Voicer.Core.Search;

/// <summary>
/// A text with what the search found in it: the words of the query and, in the text of a whole record, the passage.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Words">The words that match the query, in their order, not overlapping.</param>
/// <param name="Passage">The found passage, or <see langword="null"/> when the text is the passage itself.</param>
public sealed record MarkedText(string Text, IReadOnlyList<TextMark> Words, TextMark? Passage = null)
{
    /// <summary>
    /// Gets a value indicating whether anything in the text is marked.
    /// </summary>
    public bool HasMarks => Words.Count > 0 || Passage is not null;
}
