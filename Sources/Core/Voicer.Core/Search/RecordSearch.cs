using Voicer.Core.Summaries;

namespace Voicer.Core.Search;

/// <summary>
/// <see cref="IRecordSearch"/> that searches by words in the full-text index and by meaning in the vectors of the
/// passages, then merges both lists (<see cref="RankFusion"/>). The passages without a vector of the model are indexed
/// first, a batch at a time, so a search by meaning always covers every record.
/// </summary>
public sealed class RecordSearch : IRecordSearch
{
    /// <summary>
    /// How many passages each search gives to the fusion.
    /// </summary>
    private const int CANDIDATES = 50;

    /// <summary>
    /// How many passages are embedded in one request.
    /// </summary>
    private const int BATCH = 16;

    /// <summary>
    /// The passages and their vectors.
    /// </summary>
    private readonly IPassageIndex _index;

    /// <summary>
    /// The client of the server of the model of embeddings.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSearch"/> class.
    /// </summary>
    /// <param name="index">The passages and their vectors.</param>
    /// <param name="client">The client of the server of the model of embeddings.</param>
    public RecordSearch(IPassageIndex index, ILanguageModelClient client)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PassageHit>> SearchAsync(string query, EmbeddingSettings? embeddings, int limit, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var words = FullTextQuery.Build(query);
        IReadOnlyList<PassageHit> byWords = words is null ? [] : await _index.SearchWordsAsync(words, CANDIDATES, cancellationToken);
        if (embeddings is null)
        {
            return [.. byWords.Take(limit)];
        }

        await IndexAsync(embeddings, progress, cancellationToken);
        var vector = await _client.EmbedAsync(embeddings.Endpoint, embeddings.Model, [EmbeddingText.Query(embeddings.Model, query.Trim())], cancellationToken);
        var byMeaning = await _index.SearchVectorAsync(embeddings.Model, VectorMath.Normalize(vector[0]), CANDIDATES, cancellationToken);

        return RankFusion.Fuse([byWords, byMeaning], limit);
    }

    /// <summary>
    /// Gives a vector of the model to every passage that has none yet.
    /// </summary>
    /// <param name="embeddings">The model of embeddings.</param>
    /// <param name="progress">Receives how far the indexing is.</param>
    /// <param name="cancellationToken">Stops the work; the vectors made so far are kept.</param>
    /// <returns>A task that completes when every passage has its vector.</returns>
    private async Task IndexAsync(EmbeddingSettings embeddings, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
    {
        var total = await _index.CountWithoutVectorAsync(embeddings.Model, cancellationToken);
        var done = 0;
        while (done < total)
        {
            var batch = await _index.WithoutVectorAsync(embeddings.Model, BATCH, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            progress?.Report(new SearchProgress(done, total));
            var texts = batch.Select(p => EmbeddingText.Passage(embeddings.Model, p.Text)).ToList();
            var vectors = await _client.EmbedAsync(embeddings.Endpoint, embeddings.Model, texts, cancellationToken);
            await _index.SaveVectorsAsync(
                embeddings.Model,
                [.. batch.Select((p, i) => new PassageVector(p.PassageId, VectorMath.Normalize(vectors[i])))],
                cancellationToken);
            done += batch.Count;
        }

        if (total > 0)
        {
            progress?.Report(new SearchProgress(total, total));
        }
    }
}
