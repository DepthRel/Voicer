using Voicer.Core.Summaries;

namespace Voicer.Core.Search;

/// <summary>
/// <see cref="ITextEmbedder"/> over a model of a server of language models the user runs (LM Studio, Ollama…).
/// </summary>
public sealed class ServerTextEmbedder : ITextEmbedder
{
    /// <summary>
    /// The client of the server.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// The server and the model.
    /// </summary>
    private readonly EmbeddingSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerTextEmbedder"/> class.
    /// </summary>
    /// <param name="client">The client of the server.</param>
    /// <param name="settings">The server and the model.</param>
    public ServerTextEmbedder(ILanguageModelClient client, EmbeddingSettings settings)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public string Model => _settings.Model;

    /// <inheritdoc />
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        return _client.EmbedAsync(_settings.Endpoint, _settings.Model, texts, cancellationToken);
    }
}
