using Voicer.Core.Transcripts;

namespace Voicer.Core.Corrections;

/// <summary>
/// The text of a record with its corrections applied, and the places they changed.
/// </summary>
/// <param name="Transcript">The record with the corrected phrases; their moments stay as recognized.</param>
/// <param name="Marks">The corrected places in <see cref="Transcript.Text"/>, in their order.</param>
public sealed record CorrectedText(Transcript Transcript, IReadOnlyList<CorrectionMark> Marks);
