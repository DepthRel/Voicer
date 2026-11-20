using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// The settings: the language of the interface, the speech recognition and voice models, FFmpeg, llama.cpp and the
/// summaries. The models are listed in windows of their own; the page shows how many of them are downloaded.
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
    /// <param name="search">The search by meaning: its source, the models of Voicer and their llama-server.</param>
    /// <param name="ffmpeg">FFmpeg.</param>
    /// <param name="llamaCpp">llama.cpp, the engine of the voice models.</param>
    /// <param name="summary">The server and the model of the summaries.</param>
    /// <param name="dataDirectory">The data folder.</param>
    /// <param name="dialogs">The dialogs that open the data folder.</param>
    public SettingsViewModel(
        ILocalizer localizer,
        LanguageViewModel language,
        ModelCatalogViewModel speechModels,
        ModelCatalogViewModel voiceModels,
        SearchSettingsViewModel search,
        FfmpegItemViewModel ffmpeg,
        LlamaCppItemViewModel llamaCpp,
        SummarySettingsViewModel summary,
        string dataDirectory,
        IDialogService dialogs)
        : base(localizer)
    {
        Language = language ?? throw new ArgumentNullException(nameof(language));
        SpeechModels = speechModels ?? throw new ArgumentNullException(nameof(speechModels));
        VoiceModels = voiceModels ?? throw new ArgumentNullException(nameof(voiceModels));
        Search = search ?? throw new ArgumentNullException(nameof(search));
        Ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        LlamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
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
    /// Gets the server and the model of the summaries.
    /// </summary>
    public SummarySettingsViewModel Summary { get; }

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
