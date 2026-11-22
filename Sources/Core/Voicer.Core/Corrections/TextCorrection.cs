namespace Voicer.Core.Corrections;

/// <summary>
/// A correction of the recognized text of a record: a piece of one phrase and what replaces it. The recognized text
/// stays as it was; the correction lies over it.
/// </summary>
/// <param name="Segment">The index of the phrase in the record.</param>
/// <param name="Start">Where the replaced piece starts, in characters of the trimmed recognized text of the phrase.</param>
/// <param name="Length">How many characters the replaced piece takes.</param>
/// <param name="Original">The replaced piece as it was recognized.</param>
/// <param name="Replacement">What replaces it; empty when the piece is deleted.</param>
/// <param name="CreatedAt">When the correction was made.</param>
public sealed record TextCorrection(int Segment, int Start, int Length, string Original, string Replacement, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Gets where the replaced piece ends: the first character after it in the trimmed recognized text of the phrase.
    /// </summary>
    public int End => Start + Length;
}
