using Voicer.Core.Transcripts;

namespace Voicer.Speech.Whisper.Engine;

/// <summary>
/// Receives what a recognition reports while it runs.
/// </summary>
public interface ITranscriptionListener
{
    /// <summary>
    /// Called once the model is loaded and the recognition starts.
    /// </summary>
    void ModelLoaded();

    /// <summary>
    /// Called when the recognition advances.
    /// </summary>
    /// <param name="percent">How much of the recording is recognized, from 0 to 100.</param>
    void Progress(double percent);

    /// <summary>
    /// Called for every recognized piece of speech, in the order of the recording.
    /// </summary>
    /// <param name="segment">The piece.</param>
    void Segment(TranscriptSegment segment);
}
