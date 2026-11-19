namespace Voicer.Core.Search;

/// <summary>
/// The passages of the recognized texts, searchable by words and by meaning. The passages are made when a record is
/// kept and go with it; the vectors of their meaning are added later, per model. A failure of the storage is reported
/// as an <see cref="IOException"/>.
/// </summary>
public interface IPassageIndex
{
    /// <summary>
    /// Finds the passages that contain words, the best first.
    /// </summary>
    /// <param name="query">The query of the full-text index (<see cref="FullTextQuery"/>).</param>
    /// <param name="limit">How many passages at most.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The passages.</returns>
    Task<IReadOnlyList<PassageHit>> SearchWordsAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Counts the passages that have no vector of a model yet.
    /// </summary>
    /// <param name="model">The model of embeddings.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The number of passages.</returns>
    Task<int> CountWithoutVectorAsync(string model, CancellationToken cancellationToken);

    /// <summary>
    /// Takes passages that have no vector of a model yet.
    /// </summary>
    /// <param name="model">The model of embeddings.</param>
    /// <param name="limit">How many passages at most.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The passages; none when all have their vector.</returns>
    Task<IReadOnlyList<PassageText>> WithoutVectorAsync(string model, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the vectors of passages made by a model; a passage deleted meanwhile is skipped.
    /// </summary>
    /// <param name="model">The model of embeddings.</param>
    /// <param name="vectors">The vectors.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the vectors are kept.</returns>
    Task SaveVectorsAsync(string model, IReadOnlyList<PassageVector> vectors, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the passages whose meaning is the closest to a vector of a model.
    /// </summary>
    /// <param name="model">The model of embeddings.</param>
    /// <param name="vector">The vector of the query, of unit length.</param>
    /// <param name="limit">How many passages at most.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The passages, the closest first.</returns>
    Task<IReadOnlyList<PassageHit>> SearchVectorAsync(string model, float[] vector, int limit, CancellationToken cancellationToken);
}
