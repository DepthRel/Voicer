using Voicer.Core.Embeddings;
using Voicer.Core.Search;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Embeddings;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Services.Search;

/// <summary>
/// <see cref="ITextEmbedderSource"/> that follows the choice of the user: a downloaded model of Voicer run by its own
/// llama-server, a model of embeddings of the server of language models, or none. A choice that is not ready yet (the
/// model is not downloaded, no model of the server is chosen) searches by words only.
/// </summary>
public sealed class TextEmbedderSource : ITextEmbedderSource
{
    /// <summary>
    /// The choice of the user.
    /// </summary>
    private readonly ISearchSourceStore _source;

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models;

    /// <summary>
    /// Runs a model of Voicer.
    /// </summary>
    private readonly ILlamaEmbeddingServer _server;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences;

    /// <summary>
    /// Talks to a server of language models, the own one too.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbedderSource"/> class.
    /// </summary>
    /// <param name="source">The choice of the user.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="server">Runs a model of Voicer.</param>
    /// <param name="preferences">The settings of the server of language models.</param>
    /// <param name="client">Talks to a server of language models, the own one too.</param>
    public TextEmbedderSource(
        ISearchSourceStore source,
        IEmbeddingModelStore models,
        ILlamaEmbeddingServer server,
        ISummaryPreferencesStore preferences,
        ILanguageModelClient client)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public ITextEmbedder? Current()
    {
        return _source.Load() switch
        {
            SearchSource.Local when _models.Preferred() is { } model => new LocalTextEmbedder(_server, _client, model),
            SearchSource.Server when _preferences.Load().ToEmbeddingSettings() is { } settings => new ServerTextEmbedder(_client, settings),
            _ => null,
        };
    }
}
