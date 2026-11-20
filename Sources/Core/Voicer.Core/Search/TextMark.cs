namespace Voicer.Core.Search;

/// <summary>
/// A piece of a text to be shown marked: a found word, or the found passage in the text of its record.
/// </summary>
/// <param name="Start">Where the piece starts, in characters from the beginning of the text.</param>
/// <param name="Length">How many characters it takes.</param>
public readonly record struct TextMark(int Start, int Length)
{
    /// <summary>
    /// Gets where the piece ends: the first character after it.
    /// </summary>
    public int End => Start + Length;
}
