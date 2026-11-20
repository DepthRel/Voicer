using CommunityToolkit.Mvvm.ComponentModel;
using Voicer.Core.Embeddings;
using Voicer.Localization;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// The search by meaning in the settings: one choice of what makes its vectors (nothing, a model of Voicer, or a model
/// of the server of language models), what belongs to each choice, and why a choice does not work yet.
/// </summary>
public sealed partial class SearchSettingsViewModel : LocalizedViewModel
{
    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly ISearchSourceStore _store;

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server;

    /// <summary>
    /// Finds llama-server, which runs a model of Voicer.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp;

    /// <summary>
    /// What makes the vectors.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(IsLocal), nameof(IsServer), nameof(Warning), nameof(HasWarning))]
    private SearchSource _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSettingsViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">Remembers the choice.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="server">The settings of the server of language models.</param>
    /// <param name="llamaCpp">Finds llama-server, which runs a model of Voicer.</param>
    /// <param name="catalog">The catalog of the models of Voicer.</param>
    /// <param name="localServer">The llama-server of a model of Voicer: its state and its stop.</param>
    /// <param name="serverSettings">The settings of the server of language models, whose models of embeddings are offered.</param>
    public SearchSettingsViewModel(
        ILocalizer localizer,
        ISearchSourceStore store,
        IEmbeddingModelStore models,
        ISummaryPreferencesStore server,
        ILlamaCppLocator llamaCpp,
        ModelCatalogViewModel catalog,
        SearchServerViewModel localServer,
        SummarySettingsViewModel serverSettings)
        : base(localizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        LocalServer = localServer ?? throw new ArgumentNullException(nameof(localServer));
        ServerSettings = serverSettings ?? throw new ArgumentNullException(nameof(serverSettings));
        _source = store.Load();

        Catalog.Changed += (_, _) => RefreshWarning();
    }

    /// <summary>
    /// Gets the catalog of the models of Voicer.
    /// </summary>
    public ModelCatalogViewModel Catalog { get; }

    /// <summary>
    /// Gets the llama-server of a model of Voicer: its state and its stop.
    /// </summary>
    public SearchServerViewModel LocalServer { get; }

    /// <summary>
    /// Gets the settings of the server of language models, whose models of embeddings are offered.
    /// </summary>
    public SummarySettingsViewModel ServerSettings { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the history is searched by words only.
    /// </summary>
    public bool IsOff
    {
        get => Source == SearchSource.Off;
        set => Choose(value, SearchSource.Off);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a model of Voicer makes the vectors.
    /// </summary>
    public bool IsLocal
    {
        get => Source == SearchSource.Local;
        set => Choose(value, SearchSource.Local);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a model of the server of language models makes the vectors.
    /// </summary>
    public bool IsServer
    {
        get => Source == SearchSource.Server;
        set => Choose(value, SearchSource.Server);
    }

    /// <summary>
    /// Gets why the chosen source does not work yet, or <see langword="null"/> when it does.
    /// </summary>
    public string? Warning => Source switch
    {
        SearchSource.Local when _models.Preferred() is null => Localizer.Get("Search.Source.LocalMissing"),
        SearchSource.Local when _llamaCpp.LocateServer() is null => Localizer.Get("Search.Source.LlamaMissing"),
        SearchSource.Server when _server.Load().ToEmbeddingSettings() is null => Localizer.Get("Search.Source.ServerMissing"),
        _ => null,
    };

    /// <summary>
    /// Gets a value indicating whether a warning is shown.
    /// </summary>
    public bool HasWarning => Warning is not null;

    /// <summary>
    /// Reads again what can change outside: the llama-server of Voicer, the models installed, llama.cpp and the model of
    /// the server.
    /// </summary>
    public void Refresh()
    {
        LocalServer.Refresh();
        RefreshWarning();
    }

    /// <summary>
    /// Remembers a new choice.
    /// </summary>
    /// <param name="value">The source.</param>
    partial void OnSourceChanged(SearchSource value)
    {
        _store.Save(value);
    }

    /// <summary>
    /// Shows again why the chosen source does not work yet.
    /// </summary>
    private void RefreshWarning()
    {
        OnPropertyChanged(nameof(Warning));
        OnPropertyChanged(nameof(HasWarning));
    }

    /// <summary>
    /// Takes a checked option as the choice; an option that is unchecked changes nothing, the checked one decides.
    /// </summary>
    /// <param name="isChecked">Whether the option is checked.</param>
    /// <param name="source">The source of the option.</param>
    private void Choose(bool isChecked, SearchSource source)
    {
        if (isChecked)
        {
            Source = source;
        }
    }
}
