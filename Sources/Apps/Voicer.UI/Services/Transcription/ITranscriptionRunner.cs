using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// Recognizes the speech of a media file from start to end: extracts the sound, runs the model, collects the text.
/// </summary>
public interface ITranscriptionRunner
{
    /// <summary>
    /// Recognizes the speech of a media file.
    /// </summary>
    /// <param name="request">What to recognize, with which model and where.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the recognition.</param>
    /// <returns>The transcript.</returns>
    Task<Transcript> RunAsync(TranscriptionRequest request, IProgress<TranscriptionProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the temporary files an earlier run left behind (the application ended in the middle of a recognition).
    /// </summary>
    void ClearLeftovers();
}
