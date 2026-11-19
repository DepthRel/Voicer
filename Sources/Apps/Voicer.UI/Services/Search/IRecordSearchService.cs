using Voicer.Core.Search;

namespace Voicer.UI.Services.Search;

/// <summary>
/// Searches the records and answers questions about them with the models chosen in the settings.
/// </summary>
public interface IRecordSearchService
{
    /// <summary>
    /// Gets a value indicating whether a model of embeddings is chosen, so the search goes by meaning too.
    /// </summary>
    bool SearchesByMeaning { get; }

    /// <summary>
    /// Gets a value indicating whether a model that answers questions is chosen.
    /// </summary>
    bool CanAsk { get; }

    /// <summary>
    /// Finds the passages that best answer a query.
    /// </summary>
    /// <param name="query">What the user typed.</param>
    /// <param name="indexing">Receives how far the indexing of new passages is.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The passages, the best first.</returns>
    /// <exception cref="IOException">The storage failed.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The answer of the server cannot be read.</exception>
    Task<IReadOnlyList<PassageHit>> SearchAsync(string query, IProgress<SearchProgress>? indexing, CancellationToken cancellationToken);

    /// <summary>
    /// Answers a question from the passages of the records.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="indexing">Receives how far the indexing of new passages is.</param>
    /// <param name="answerSoFar">Receives the answer as it arrives.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The answer with its sources.</returns>
    /// <exception cref="InvalidOperationException">No model that answers questions is chosen.</exception>
    /// <exception cref="IOException">The storage failed.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The answer of the server cannot be read, or is empty.</exception>
    Task<QuestionAnswer> AskAsync(string question, IProgress<SearchProgress>? indexing, IProgress<string>? answerSoFar, CancellationToken cancellationToken);
}
