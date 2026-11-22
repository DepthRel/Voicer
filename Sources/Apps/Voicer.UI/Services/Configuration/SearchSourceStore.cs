using Voicer.Core.Embeddings;
using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="ISearchSourceStore"/> in the settings of the user. Before the first choice: a downloaded model of Voicer,
/// else a model of embeddings chosen on the server, else none.
/// </summary>
public sealed class SearchSourceStore : ModelSourceStore, ISearchSourceStore
{
    /// <summary>
    /// The key of the choice in the settings.
    /// </summary>
    private const string SOURCE = "search.source";

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSourceStore"/> class.
    /// </summary>
    /// <param name="settings">The settings of the user.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="server">The settings of the server of language models.</param>
    public SearchSourceStore(ISettingsStore settings, IEmbeddingModelStore models, ISummaryPreferencesStore server)
        : base(settings, SOURCE)
    {
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <inheritdoc />
    protected override ModelSource Guess()
    {
        if (_models.Preferred() is not null)
        {
            return ModelSource.Local;
        }

        return _server.Load().ToEmbeddingSettings() is not null ? ModelSource.Server : ModelSource.Off;
    }
}
