namespace Voicer.Core.Transcripts;

/// <summary>
/// A stretch of a sound where someone speaks, as the detector of speech found it.
/// </summary>
/// <param name="Start">Where the speech begins.</param>
/// <param name="End">Where the speech ends.</param>
public sealed record SpeechSpan(TimeSpan Start, TimeSpan End);
