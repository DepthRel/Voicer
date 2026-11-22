using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// The settings: the language of the interface, the speech recognition and voice models, the connection to a server of
/// language models, the models of the summaries and of the search, FFmpeg and llama.cpp. The models are listed in windows of their own; the page shows how many of them are downloaded.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModelBase
{
    /// <summary>
    /// The dialogs that open the data folder.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="language">The language selector.</param>
    /// <param name="speechModels">The catalog of the speech recognition models.</param>
    /// <param name="voiceModels">The catalog of the voice models.</param>
    /// <param name="connection">The connection to a server of language models, local or in the cloud.</param>
    /// <param name="summaries">The model of the summaries and the answers: its source, the models of Voicer and their llama-server.</param>
    /// <param name="search">The search by meaning: its source, the models of Voicer and their llama-server.</param>
    /// <param name="ffmpeg">FFmpeg.</param>
    /// <param name="llamaCpp">llama.cpp, the engine of the voice models.</param>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="dialogs">The dialogs that open the data folder.</param>
    public SettingsViewModel(
        ILocalizer localizer,
        LanguageViewModel language,
        ModelCatalogViewModel speechModels,
        ModelCatalogViewModel voiceModels,
        LanguageServerSettingsViewModel connection,
        SummaryModelSettingsViewModel summaries,
        SearchSettingsViewModel search,
        FfmpegItemViewModel ffmpeg,
        LlamaCppItemViewModel llamaCpp,
        string dataDirectory,
        IDialogService dialogs)
        : base(localizer)
    {
        Language = language ?? throw new ArgumentNullException(nameof(language));
        SpeechModels = speechModels ?? throw new ArgumentNullException(nameof(speechModels));
        VoiceModels = voiceModels ?? throw new ArgumentNullException(nameof(voiceModels));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        Search = search ?? throw new ArgumentNullException(nameof(search));
        Ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        LlamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = dataDirectory;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    /// <summary>
    /// Gets the language selector.
    /// </summary>
    public LanguageViewModel Language { get; }

    /// <summary>
    /// Gets the catalog of the speech recognition models.
    /// </summary>
    public ModelCatalogViewModel SpeechModels { get; }

    /// <summary>
    /// Gets the catalog of the voice models.
    /// </summary>
    public ModelCatalogViewModel VoiceModels { get; }

    /// <summary>
    /// Gets the connection to a server of language models, local or in the cloud.
    /// </summary>
    public LanguageServerSettingsViewModel Connection { get; }

    /// <summary>
    /// Gets the model of the summaries and the answers: its source, the models of Voicer and their llama-server.
    /// </summary>
    public SummaryModelSettingsViewModel Summaries { get; }

    /// <summary>
    /// Gets the search by meaning: its source, the models of Voicer and their llama-server.
    /// </summary>
    public SearchSettingsViewModel Search { get; }

    /// <summary>
    /// Gets FFmpeg.
    /// </summary>
    public FfmpegItemViewModel Ffmpeg { get; }

    /// <summary>
    /// Gets llama.cpp, the engine of the voice models.
    /// </summary>
    public LlamaCppItemViewModel LlamaCpp { get; }

    /// <summary>
    /// Gets the data folder.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets the version of the application.
    /// </summary>
    public string VersionText => Localizer.Format("Settings.Version", Version);

    /// <summary>
    /// Gets the version of the application, without the build metadata.
    /// </summary>
    private static string Version
    {
        get
        {
            var version = typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var plus = version.IndexOf('+', StringComparison.Ordinal);

            return plus < 0 ? version : version[..plus];
        }
    }

    /// <inheritdoc />
    public override void Activate()
    {
        Ffmpeg.RefreshCommand.Execute(null);
        LlamaCpp.Refresh();
        Summaries.Refresh();
        Search.Refresh();
    }

    /// <summary>
    /// Opens the data folder in the file manager.
    /// </summary>
    /// <returns>A task that completes when the folder is opened.</returns>
    [RelayCommand]
    private Task OpenDataFolderAsync()
    {
        return _dialogs.OpenFolderAsync(DataDirectory);
    }
}
