namespace Voicer.Core.Transcripts;

/// <summary>
/// A recognized piece of speech.
/// </summary>
/// <param name="Start">Where the piece starts in the recording.</param>
/// <param name="End">Where the piece ends in the recording.</param>
/// <param name="Text">The recognized text.</param>
public sealed record TranscriptSegment(TimeSpan Start, TimeSpan End, string Text);
