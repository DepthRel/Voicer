using Voicer.Speech.Llama.Enums;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// The state of the llama-server of the search by meaning at a moment.
/// </summary>
/// <param name="State">What the server is doing.</param>
/// <param name="ModelName">The name of its model, or <see langword="null"/> when none runs.</param>
/// <param name="Usage">What its process takes, or <see langword="null"/> when none runs.</param>
/// <param name="StopsAt">When it stops by itself if no search comes, or <see langword="null"/> when it is not waiting.</param>
public sealed record EmbeddingServerStatus(EmbeddingServerState State, string? ModelName, ServerUsage? Usage, DateTimeOffset? StopsAt)
{
    /// <summary>
    /// Gets the state of no server.
    /// </summary>
    public static EmbeddingServerStatus Stopped { get; } = new(EmbeddingServerState.Stopped, null, null, null);
}
