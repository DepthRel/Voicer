using Voicer.Core.Search;

namespace Voicer.Core.Corrections;

/// <summary>
/// What a correction is about to replace: a piece of one phrase, in the recognized text and as it is shown now.
/// </summary>
/// <param name="Segment">The index of the phrase in the record.</param>
/// <param name="Start">Where the piece starts, in characters of the trimmed recognized text of the phrase.</param>
/// <param name="Length">How many characters of the recognized text the piece takes.</param>
/// <param name="Shown">Where the piece lies in the corrected text of the record.</param>
/// <param name="ShownText">The piece as it is shown now, with the corrections made in it.</param>
public sealed record CorrectionTarget(int Segment, int Start, int Length, TextMark Shown, string ShownText)
{
    /// <summary>
    /// Gets where the piece ends: the first character after it in the trimmed recognized text of the phrase.
    /// </summary>
    public int End => Start + Length;
}
