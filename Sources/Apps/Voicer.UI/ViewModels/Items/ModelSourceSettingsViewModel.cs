using CommunityToolkit.Mvvm.ComponentModel;
using Voicer.Localization;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// One choice of what runs a model the application needs, in the settings: nothing, a model of Voicer (its catalog and
/// its llama-server), or a model of the server of language models; and why the choice does not work yet. A model of
/// Voicer that is left for another choice is unloaded at once.
/// </summary>
public abstract partial class ModelSourceSettingsViewModel : LocalizedViewModel
{
    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly IModelSourceStore _store;

    /// <summary>
    /// What runs the model.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff), nameof(IsLocal), nameof(IsServer), nameof(Warning), nameof(HasWarning))]
    private ModelSource _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSourceSettingsViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">Remembers the choice.</param>
    /// <param name="catalog">The catalog of the models of Voicer.</param>
    /// <param name="localServer">The llama-server of a model of Voicer: its state and its stop.</param>
    /// <param name="serverSettings">The connection to the server of language models, whose models are offered.</param>
    protected ModelSourceSettingsViewModel(
        ILocalizer localizer,
        IModelSourceStore store,
        ModelCatalogViewModel catalog,
        LlamaServerViewModel localServer,
        LanguageServerSettingsViewModel serverSettings)
        : base(localizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
    public LlamaServerViewModel LocalServer { get; }

    /// <summary>
    /// Gets the connection to the server of language models, whose models are offered.
    /// </summary>
    public LanguageServerSettingsViewModel ServerSettings { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the feature is off.
    /// </summary>
    public bool IsOff
    {
        get => Source == ModelSource.Off;
        set => Choose(value, ModelSource.Off);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a model of Voicer runs.
    /// </summary>
    public bool IsLocal
    {
        get => Source == ModelSource.Local;
        set => Choose(value, ModelSource.Local);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a model of the server of language models runs.
    /// </summary>
    public bool IsServer
    {
        get => Source == ModelSource.Server;
        set => Choose(value, ModelSource.Server);
    }

    /// <summary>
    /// Gets why the chosen source does not work yet, or <see langword="null"/> when it does.
    /// </summary>
    public abstract string? Warning { get; }

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
    /// Remembers a new choice, and unloads a model of Voicer that is no longer chosen.
    /// </summary>
    /// <param name="value">The source.</param>
    partial void OnSourceChanged(ModelSource value)
    {
        _store.Save(value);

        if (value != ModelSource.Local && LocalServer.CanStop)
        {
            LocalServer.Stop();
        }
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
    private void Choose(bool isChecked, ModelSource source)
    {
        if (isChecked)
        {
            Source = source;
        }
    }
}
