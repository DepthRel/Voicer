using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.Generation;

/// <summary>
/// A generated speech: the recording and its text with the moment of every piece.
/// </summary>
/// <param name="AudioPath">The WAV file of the whole speech; it lives in the temporary folder until the next generation.</param>
/// <param name="Duration">How long the speech lasts.</param>
/// <param name="Transcript">The spoken text, a fragment per piece, for the history.</param>
public sealed record GenerationResult(string AudioPath, TimeSpan Duration, Transcript Transcript);
