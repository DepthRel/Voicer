using Voicer.Core.Embeddings;
using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="ISearchSourceStore"/> in the settings of the user. Before the first choice the source follows what is
/// already there, so an update changes nothing: a downloaded model of Voicer, else a model of embeddings chosen on the
/// server, else none.
/// </summary>
public sealed class SearchSourceStore : ISearchSourceStore
{
    /// <summary>
    /// The key of the choice in the settings.
    /// </summary>
    private const string SOURCE = "search.source";

    /// <summary>
    /// The settings of the user.
    /// </summary>
    private readonly ISettingsStore _settings;

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
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    /// <inheritdoc />
    public SearchSource Load()
    {
        if (Enum.TryParse<SearchSource>(_settings.Get(SOURCE), ignoreCase: false, out var source) && Enum.IsDefined(source))
        {
            return source;
        }

        if (_models.Preferred() is not null)
        {
            return SearchSource.Local;
        }

        return _server.Load().ToEmbeddingSettings() is not null ? SearchSource.Server : SearchSource.Off;
    }

    /// <inheritdoc />
    public void Save(SearchSource source)
    {
        _settings.Set(SOURCE, source.ToString());
    }
}
