using Voicer.Core.Transcripts;

namespace Voicer.Core.Corrections;

/// <summary>
/// The text of a record as it was recognized, with the corrections the user made over it.
/// </summary>
/// <param name="Original">The record as it was recognized.</param>
/// <param name="Corrections">The corrections.</param>
public sealed record RecordText(Transcript Original, IReadOnlyList<TextCorrection> Corrections)
{
    /// <summary>
    /// Applies the corrections to the recognized text.
    /// </summary>
    /// <returns>The corrected text with its corrected places.</returns>
    public CorrectedText Apply()
    {
        return TranscriptCorrections.Apply(Original, Corrections);
    }
}
