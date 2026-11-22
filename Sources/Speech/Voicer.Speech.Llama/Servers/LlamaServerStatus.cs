using Voicer.Speech.Llama.Enums;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// The state of a llama-server of the application at a moment.
/// </summary>
/// <param name="State">What the server is doing.</param>
/// <param name="ModelName">The name of its model, or <see langword="null"/> when none runs.</param>
/// <param name="Usage">What its process takes, or <see langword="null"/> when none runs.</param>
/// <param name="StopsAt">When it stops by itself if no request comes, or <see langword="null"/> when it is not waiting.</param>
public sealed record LlamaServerStatus(LlamaServerState State, string? ModelName, ServerUsage? Usage, DateTimeOffset? StopsAt)
{
    /// <summary>
    /// Gets the state of no server.
    /// </summary>
    public static LlamaServerStatus Stopped { get; } = new(LlamaServerState.Stopped, null, null, null);
}
