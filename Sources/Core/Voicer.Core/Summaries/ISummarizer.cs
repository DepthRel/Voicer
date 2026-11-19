using Voicer.Core.Transcripts;

namespace Voicer.Core.Summaries;

/// <summary>
/// Writes the summary of a recognized text with a language model.
/// </summary>
public interface ISummarizer
{
    /// <summary>
    /// Summarizes a recognized text, in the language of the interface.
    /// </summary>
    /// <param name="transcript">The recognized text.</param>
    /// <param name="settings">The server, the model and its context.</param>
    /// <param name="progress">Receives how far the summary is, and the final summary as it arrives.</param>
    /// <param name="cancellationToken">Stops the requests.</param>
    /// <returns>The summary, without the reasoning of a thinking model.</returns>
    /// <exception cref="InvalidDataException">The text has no speech, or the model gave no answer.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    Task<string> SummarizeAsync(Transcript transcript, SummarySettings settings, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken);
}
