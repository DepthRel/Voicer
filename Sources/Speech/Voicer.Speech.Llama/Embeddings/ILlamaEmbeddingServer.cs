using Voicer.Core.Embeddings;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// Keeps llama-server with a model of embeddings running while the search needs it.
/// </summary>
public interface ILlamaEmbeddingServer
{
    /// <summary>
    /// Takes a server of the model for a request, starting it when none runs. The server is not ended for a pause
    /// while the lease is held; the pause starts when the last lease is given back.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">Stops the waiting for the start; the process started is ended.</param>
    /// <returns>The lease, with the address of the OpenAI-compatible API of the server and its key.</returns>
    /// <exception cref="FileNotFoundException">llama.cpp or the model is not installed.</exception>
    /// <exception cref="InvalidDataException">The server could not be started or did not get ready in time.</exception>
    Task<EmbeddingServerLease> AcquireAsync(EmbeddingModel model, CancellationToken cancellationToken);
}
