using Voicer.Core.Models;

namespace Voicer.Core.Embeddings;

/// <summary>
/// A model of embeddings the application downloads and runs itself for the search by meaning: one GGUF file of a
/// pinned revision, checked by its size and SHA-256.
/// </summary>
/// <param name="Id">The identifier: the name of its folder in <c>Data/Models</c> and of the model in the requests.</param>
/// <param name="Name">The name shown to the user.</param>
/// <param name="FileName">The name of the file of the weights.</param>
/// <param name="DownloadUri">Where the file is downloaded from, over HTTPS.</param>
/// <param name="SizeBytes">The exact size of the file.</param>
/// <param name="Sha256">The SHA-256 of the file, in lower-case hex.</param>
/// <param name="SlotTokens">How many tokens one text may take: the context of a slot of the server.</param>
public sealed record EmbeddingModel(
    string Id,
    string Name,
    string FileName,
    Uri DownloadUri,
    long SizeBytes,
    string Sha256,
    int SlotTokens) : IServerModel
{
    /// <summary>
    /// The prefix that keeps the vectors of the models of the application apart from those of a server of the same name.
    /// </summary>
    private const string VECTOR_PREFIX = "voicer/";

    /// <summary>
    /// How many tokens of a slot the model adds around a text (the marks of its beginning and end), with a margin.
    /// </summary>
    private const int SPECIAL_TOKENS = 8;

    /// <inheritdoc />
    public string DescriptionKey => $"EmbeddingModel.{Id}.Description";

    /// <summary>
    /// Gets the name the vectors of the model are kept under in the history.
    /// </summary>
    public string VectorName => VECTOR_PREFIX + Id;

    /// <summary>
    /// Gets how long a text may be so that it surely fits into a slot; a longer one is cut. A character makes one token
    /// at most (the vocabulary has no tokens of single bytes), so a text of a few characters fewer than the slot fits
    /// even when no two of its characters join into one token.
    /// </summary>
    public int MaxTextLength => SlotTokens - SPECIAL_TOKENS;
}
