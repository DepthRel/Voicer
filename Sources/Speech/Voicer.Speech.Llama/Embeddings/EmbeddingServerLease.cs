using Voicer.Core.Summaries;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// The use of a running llama-server: while it is held, the server is not ended for a pause; disposing it gives the
/// server back.
/// </summary>
public sealed class EmbeddingServerLease : IDisposable
{
    /// <summary>
    /// Gives the server back, once.
    /// </summary>
    private Action? _release;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingServerLease"/> class.
    /// </summary>
    /// <param name="endpoint">The address of the server, with its key.</param>
    /// <param name="release">Gives the server back.</param>
    public EmbeddingServerLease(LanguageModelEndpoint endpoint, Action release)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <summary>
    /// Gets the address of the OpenAI-compatible API of the server, with its key.
    /// </summary>
    public LanguageModelEndpoint Endpoint { get; }

    /// <summary>
    /// Gives the server back; a second call does nothing.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
