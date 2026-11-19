namespace Voicer.Core.Search;

/// <summary>
/// Prepares a text for a model of embeddings: some families were trained to tell a query from a passage by a prefix
/// (multilingual-e5: <c>query: </c> and <c>passage: </c>; nomic-embed-text: <c>search_query: </c> and
/// <c>search_document: </c>); the others (bge-m3 and most) take the text as it is.
/// </summary>
public static class EmbeddingText
{
    /// <summary>
    /// Prepares a query.
    /// </summary>
    /// <param name="model">The identifier of the model.</param>
    /// <param name="text">The query.</param>
    /// <returns>The text to embed.</returns>
    public static string Query(string model, string text)
    {
        return Prefixes(model).Query + text;
    }

    /// <summary>
    /// Prepares a passage.
    /// </summary>
    /// <param name="model">The identifier of the model.</param>
    /// <param name="text">The passage.</param>
    /// <returns>The text to embed.</returns>
    public static string Passage(string model, string text)
    {
        return Prefixes(model).Passage + text;
    }

    /// <summary>
    /// Finds the prefixes of a model by its name.
    /// </summary>
    /// <param name="model">The identifier of the model.</param>
    /// <returns>The prefix of a query and of a passage; empty when the model needs none.</returns>
    private static (string Query, string Passage) Prefixes(string model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var name = model.ToLowerInvariant();
        if (name.Contains("-e5", StringComparison.Ordinal) || name.StartsWith("e5-", StringComparison.Ordinal) || name.Contains("/e5-", StringComparison.Ordinal))
        {
            return ("query: ", "passage: ");
        }

        if (name.Contains("nomic", StringComparison.Ordinal))
        {
            return ("search_query: ", "search_document: ");
        }

        return (string.Empty, string.Empty);
    }
}
