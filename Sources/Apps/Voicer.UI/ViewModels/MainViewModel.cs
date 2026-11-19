using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.UI.Enums;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.ViewModels;

/// <summary>
/// The main window: the tabs and the page of the selected one.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// The selected tab.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranscriptionSelected), nameof(IsGenerationSelected), nameof(IsHistorySelected), nameof(IsSettingsSelected), nameof(CurrentPage))]
    private TabKey _selectedTab;

    /// <summary>
    /// The page of the Settings tab, set once it exists (the transcription page opens the settings).
    /// </summary>
    private SettingsViewModel? _settings;

    /// <summary>
    /// The page of the Transcription tab.
    /// </summary>
    private TranscriptionViewModel? _transcription;

    /// <summary>
    /// The page of the Generation tab.
    /// </summary>
    private GenerationViewModel? _generation;

    /// <summary>
    /// The page of the History tab.
    /// </summary>
    private HistoryViewModel? _history;

    /// <summary>
    /// Gets the page of the Transcription tab.
    /// </summary>
    public TranscriptionViewModel Transcription => _transcription ?? throw new InvalidOperationException("The pages are not attached.");

    /// <summary>
    /// Gets the page of the Generation tab.
    /// </summary>
    public GenerationViewModel Generation => _generation ?? throw new InvalidOperationException("The pages are not attached.");

    /// <summary>
    /// Gets the page of the History tab.
    /// </summary>
    public HistoryViewModel History => _history ?? throw new InvalidOperationException("The pages are not attached.");

    /// <summary>
    /// Gets the page of the Settings tab.
    /// </summary>
    public SettingsViewModel Settings => _settings ?? throw new InvalidOperationException("The pages are not attached.");

    /// <summary>
    /// Gets a value indicating whether the Transcription tab is selected.
    /// </summary>
    public bool IsTranscriptionSelected => SelectedTab == TabKey.Transcription;

    /// <summary>
    /// Gets a value indicating whether the Generation tab is selected.
    /// </summary>
    public bool IsGenerationSelected => SelectedTab == TabKey.Generation;

    /// <summary>
    /// Gets a value indicating whether the History tab is selected.
    /// </summary>
    public bool IsHistorySelected => SelectedTab == TabKey.History;

    /// <summary>
    /// Gets a value indicating whether the Settings tab is selected.
    /// </summary>
    public bool IsSettingsSelected => SelectedTab == TabKey.Settings;

    /// <summary>
    /// Gets the page of the selected tab.
    /// </summary>
    public PageViewModelBase CurrentPage => SelectedTab switch
    {
        TabKey.Generation => Generation,
        TabKey.History => History,
        TabKey.Settings => Settings,
        _ => Transcription,
    };

    /// <summary>
    /// Attaches the pages and shows the first one. The pages are created after this view model, because the
    /// transcription page opens the settings through it.
    /// </summary>
    /// <param name="transcription">The page of the Transcription tab.</param>
    /// <param name="generation">The page of the Generation tab.</param>
    /// <param name="history">The page of the History tab.</param>
    /// <param name="settings">The page of the Settings tab.</param>
    public void Attach(TranscriptionViewModel transcription, GenerationViewModel generation, HistoryViewModel history, SettingsViewModel settings)
    {
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _generation = generation ?? throw new ArgumentNullException(nameof(generation));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        settings.SpeechModels.Installed += (_, _) => transcription.RefreshModels();
        settings.VoiceModels.Installed += (_, _) => generation.RefreshModels();

        OnPropertyChanged(nameof(CurrentPage));
        CurrentPage.Activate();
    }

    /// <summary>
    /// Shows the page of a tab.
    /// </summary>
    /// <param name="tab">The tab.</param>
    [RelayCommand]
    public void SelectTab(TabKey tab)
    {
        SelectedTab = tab;
        CurrentPage.Activate();
    }
}
