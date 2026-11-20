namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// Shows the llama-server of the search by meaning to the user and lets them end it.
/// </summary>
public interface IEmbeddingServerMonitor
{
    /// <summary>
    /// Reads the state of the server and what its process takes now.
    /// </summary>
    /// <returns>The state.</returns>
    EmbeddingServerStatus Status();

    /// <summary>
    /// Ends the server at once, giving its memory back, unless it is starting or a search uses it.
    /// </summary>
    /// <returns><see langword="true"/> when no server runs any more; <see langword="false"/> when it is in use.</returns>
    bool TryStop();
}
