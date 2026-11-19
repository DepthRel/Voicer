using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// What the worker recognized.
/// </summary>
/// <param name="Segments">The recognized pieces of speech, in the order of the recording.</param>
/// <param name="Language">The language the model detected, or <see langword="null"/>.</param>
public sealed record WorkerTranscription(IReadOnlyList<TranscriptSegment> Segments, string? Language);
