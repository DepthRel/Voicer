namespace Voicer.Core.Search;

/// <summary>
/// Merges the lists of the search by words and the search by meaning by Reciprocal Rank Fusion: a passage scores
/// <c>1 / (K + rank)</c> in every list it is in, so the passages both searches like come first, and neither list needs
/// scores comparable with the other.
/// </summary>
public static class RankFusion
{
    /// <summary>
    /// The constant of the fusion; 60 is the value of the original paper and of the hybrid search of Elasticsearch.
    /// </summary>
    private const int K = 60;

    /// <summary>
    /// Merges ranked lists.
    /// </summary>
    /// <param name="lists">The lists, each the best first.</param>
    /// <param name="limit">How many passages at most.</param>
    /// <returns>The passages, the best first; a tie keeps the order of the first list it appears in.</returns>
    public static IReadOnlyList<PassageHit> Fuse(IReadOnlyList<IReadOnlyList<PassageHit>> lists, int limit)
    {
        ArgumentNullException.ThrowIfNull(lists);

        var scores = new Dictionary<long, (PassageHit Hit, double Score, int Order)>();
        var order = 0;
        foreach (var list in lists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var hit = list[rank];
                var score = 1.0 / (K + rank + 1);
                scores[hit.PassageId] = scores.TryGetValue(hit.PassageId, out var known)
                    ? (known.Hit, known.Score + score, known.Order)
                    : (hit, score, order++);
            }
        }

        return [.. scores.Values
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Order)
            .Take(limit)
            .Select(s => s.Hit)];
    }
}
