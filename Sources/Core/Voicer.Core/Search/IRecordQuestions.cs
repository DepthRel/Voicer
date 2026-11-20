using Voicer.Core.Summaries;

namespace Voicer.Core.Search;

/// <summary>
/// Answers questions about the records: finds the passages that matter and lets a language model answer from them only
/// (retrieval-augmented generation).
/// </summary>
public interface IRecordQuestions
{
    /// <summary>
    /// Answers a question.
    /// </summary>
    /// <param name="question">The question, in any words.</param>
    /// <param name="chat">The server and the model that write the answer.</param>
    /// <param name="embedder">The model of the search by meaning, or <see langword="null"/> to find passages by words.</param>
    /// <param name="indexing">Receives how far the indexing of new passages is.</param>
    /// <param name="answerSoFar">Receives the answer as it arrives.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The answer with its sources; without any passage found, a note and no sources.</returns>
    /// <exception cref="IOException">The storage failed.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The answer of the server cannot be read, or is empty.</exception>
    Task<QuestionAnswer> AskAsync(
        string question,
        SummarySettings chat,
        ITextEmbedder? embedder,
        IProgress<SearchProgress>? indexing,
        IProgress<string>? answerSoFar,
        CancellationToken cancellationToken);
}
