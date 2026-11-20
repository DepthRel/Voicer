namespace Voicer.Core.Search;

/// <summary>
/// Finds passages of the recognized texts by words and, with a model of embeddings, by meaning.
/// </summary>
public interface IRecordSearch
{
    /// <summary>
    /// Finds the passages that best answer a query.
    /// </summary>
    /// <param name="query">What the user typed: words, phrases in quotes, or a question.</param>
    /// <param name="embedder">The model of the search by meaning, or <see langword="null"/> to search by words only.</param>
    /// <param name="limit">How many passages at most.</param>
    /// <param name="progress">Receives how far the indexing of new passages is, before a search by meaning.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The passages, the best first.</returns>
    /// <exception cref="IOException">The storage failed.</exception>
    /// <exception cref="HttpRequestException">The model cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The answer cannot be read, or the model could not be started.</exception>
    Task<IReadOnlyList<PassageHit>> SearchAsync(string query, ITextEmbedder? embedder, int limit, IProgress<SearchProgress>? progress, CancellationToken cancellationToken);
}
