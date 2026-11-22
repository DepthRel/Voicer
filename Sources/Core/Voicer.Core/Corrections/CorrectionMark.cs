using Voicer.Core.Search;

namespace Voicer.Core.Corrections;

/// <summary>
/// A corrected place in the corrected text of a record.
/// </summary>
/// <param name="Range">
/// Where the replacement lies in the corrected text; for a deletion, the one character where the deleted piece was.
/// </param>
/// <param name="Correction">The correction made there.</param>
public sealed record CorrectionMark(TextMark Range, TextCorrection Correction);
