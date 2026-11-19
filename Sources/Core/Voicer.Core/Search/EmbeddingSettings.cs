using Voicer.Core.Summaries;

namespace Voicer.Core.Search;

/// <summary>
/// What the search by meaning is made with: the server and its model of embeddings.
/// </summary>
/// <param name="Endpoint">The server of language models.</param>
/// <param name="Model">The identifier of the model of embeddings; a multilingual one (bge-m3, multilingual-e5) for
/// Russian speech.</param>
public sealed record EmbeddingSettings(LanguageModelEndpoint Endpoint, string Model);
