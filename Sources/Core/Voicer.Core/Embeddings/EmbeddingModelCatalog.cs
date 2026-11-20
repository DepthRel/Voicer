namespace Voicer.Core.Embeddings;

/// <summary>
/// The models of embeddings the application offers for the search by meaning, each pinned to a revision of its
/// repository, a size and a SHA-256, and checked with the pinned build of llama.cpp.
/// </summary>
public static class EmbeddingModelCatalog
{
    /// <summary>
    /// The address of the files of bge-m3 (MIT) converted to GGUF by the llama.cpp project, at a pinned revision.
    /// </summary>
    private const string BGE_M3 = "https://huggingface.co/ggml-org/bge-m3-Q8_0-GGUF/resolve/9eba04c5d75ba5a1595e45de734d36bef4e5cb98/";

    /// <summary>
    /// Gets bge-m3: a multilingual model of BAAI, in Q8_0.
    /// </summary>
    public static EmbeddingModel BgeM3 { get; } = new(
        "bge-m3",
        "bge-m3",
        "bge-m3-q8_0.gguf",
        new Uri(BGE_M3 + "bge-m3-q8_0.gguf"),
        634_553_760,
        "aa473d51f451a22f0fcf39ba3330c14bed38a385712b1113440f69df4047a173",
        SlotTokens: 2048);

    /// <summary>
    /// Gets every model of the catalog, the preferred first: when several are installed, the first is used.
    /// </summary>
    public static IReadOnlyList<EmbeddingModel> All { get; } = [BgeM3];
}
