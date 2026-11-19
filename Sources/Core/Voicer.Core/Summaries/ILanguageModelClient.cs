namespace Voicer.Core.Summaries;

/// <summary>
/// Talks to a server of language models. Its failures are an <see cref="HttpRequestException"/> (the server cannot be
/// reached or refused the request) or an <see cref="InvalidDataException"/> (the answer cannot be read), worded for the
/// user.
/// </summary>
public interface ILanguageModelClient
{
    /// <summary>
    /// Lists the models the server offers.
    /// </summary>
    /// <param name="endpoint">The server.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The identifiers of the models, sorted.</returns>
    Task<IReadOnlyList<string>> ListModelsAsync(LanguageModelEndpoint endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Turns texts into vectors of their meaning with a model of embeddings.
    /// </summary>
    /// <param name="endpoint">The server.</param>
    /// <param name="model">The identifier of the model of embeddings.</param>
    /// <param name="inputs">The texts.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>A vector per text, in their order.</returns>
    Task<IReadOnlyList<float[]>> EmbedAsync(LanguageModelEndpoint endpoint, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken);

    /// <summary>
    /// Asks a model to answer a conversation; the answer comes piece by piece.
    /// </summary>
    /// <param name="endpoint">The server.</param>
    /// <param name="model">The identifier of the model.</param>
    /// <param name="messages">The conversation.</param>
    /// <param name="answerSoFar">Receives the whole answer received so far after each piece.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The whole answer.</returns>
    Task<string> CompleteAsync(
        LanguageModelEndpoint endpoint,
        string model,
        IReadOnlyList<ChatMessage> messages,
        IProgress<string>? answerSoFar,
        CancellationToken cancellationToken);
}
