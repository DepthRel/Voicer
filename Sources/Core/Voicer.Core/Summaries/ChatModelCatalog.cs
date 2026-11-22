namespace Voicer.Core.Summaries;

/// <summary>
/// The light language models of Voicer for summaries and answers, the stronger first: multilingual models under the
/// Apache-2.0 license, quantized to Q4_K_M, each pinned to a revision, a size and a SHA-256.
/// </summary>
public static class ChatModelCatalog
{
    /// <summary>
    /// The context every model is loaded with: a summary of a long text is made part by part within it, and it keeps the
    /// memory the model takes beside its weights small.
    /// </summary>
    private const int CONTEXT_TOKENS = 8192;

    /// <summary>
    /// The pinned revision of Qwen3-4B-Instruct-2507 in GGUF.
    /// </summary>
    private const string QWEN3_4B = "https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF/resolve/a06e946bb6b655725eafa393f4a9745d460374c9/";

    /// <summary>
    /// The pinned revision of Qwen3-1.7B in GGUF.
    /// </summary>
    private const string QWEN3_1_7B = "https://huggingface.co/unsloth/Qwen3-1.7B-GGUF/resolve/d7f544eead698dbd1f15126ef60b45a1e1933222/";

    /// <summary>
    /// Gets Qwen3-4B-Instruct-2507 by Alibaba Cloud: the stronger model, answers at once without reasoning aloud.
    /// </summary>
    public static ChatModel Qwen3Instruct4B { get; } = new(
        "qwen3-4b-instruct-2507",
        "Qwen3-4B-Instruct-2507",
        "Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
        new Uri(QWEN3_4B + "Qwen3-4B-Instruct-2507-Q4_K_M.gguf"),
        2_497_281_120,
        "3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597",
        CONTEXT_TOKENS,
        Thinks: false);

    /// <summary>
    /// Gets Qwen3-1.7B by Alibaba Cloud: the lighter model for a computer with little memory; told not to reason aloud.
    /// </summary>
    public static ChatModel Qwen3Small { get; } = new(
        "qwen3-1.7b",
        "Qwen3-1.7B",
        "Qwen3-1.7B-Q4_K_M.gguf",
        new Uri(QWEN3_1_7B + "Qwen3-1.7B-Q4_K_M.gguf"),
        1_107_409_472,
        "b139949c5bd74937ad8ed8c8cf3d9ffb1e99c866c823204dc42c0d91fa181897",
        CONTEXT_TOKENS,
        Thinks: true);

    /// <summary>
    /// Gets every model, the stronger first: the summaries use the first one installed.
    /// </summary>
    public static IReadOnlyList<ChatModel> All { get; } = [Qwen3Instruct4B, Qwen3Small];
}
