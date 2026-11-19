using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Voicer.Audio;
using Voicer.Audio.OpenAl;
using Voicer.Core.Models;
using Voicer.Core.Paths;
using Voicer.Core.Playback;
using Voicer.Core.Search;
using Voicer.Core.Summaries;
using Voicer.Core.Synthesis;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.LanguageModels;
using Voicer.Localization;
using Voicer.Media;
using Voicer.Media.Ffmpeg;
using Voicer.Speech.Llama.Engine;
using Voicer.Speech.Llama.Tools;
using Voicer.Storage;
using Voicer.UI.Enums;
using Voicer.UI.Localization;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Diagnostics;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.Generation;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Localization;
using Voicer.UI.Services.Search;
using Voicer.UI.Services.Summaries;
using Voicer.UI.Services.Transcription;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;
using Voicer.UI.ViewModels.Pages;
using Voicer.UI.Views;

namespace Voicer.UI;

/// <summary>
/// The Avalonia application: chooses the data folder and the language, composes the services and the view models and
/// opens the main window.
/// </summary>
public sealed class App : Application
{
    /// <summary>
    /// The client of the downloads; it lives as long as the application.
    /// </summary>
    private HttpClient? _http;

    /// <summary>
    /// Whether the notice of an unexpected failure is on the screen, so a failure that repeats does not stack them.
    /// </summary>
    private bool _showingFailure;

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    /// <remarks>
    /// The localizer is created here, before the first view is loaded, and handed to everything that words a text.
    /// </remarks>
    public App()
    {
        Localizer = new Localizer(LanguageCatalog.LoadEmbedded());
        Localization = new LocalizationSource(Localizer);
    }

    /// <summary>
    /// Gets the localizer of the user interface.
    /// </summary>
    public ILocalizer Localizer { get; }

    /// <summary>
    /// Gets the texts of the current language as a source of bindings; the views reach it through
    /// <see cref="LocExtension"/>.
    /// </summary>
    public LocalizationSource Localization { get; }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = AppPaths.Resolve(
                AppContext.BaseDirectory,
                desktop.Args ?? [],
                AppPaths.CanWrite,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            // The first start takes the language of the operating system; the choice is kept in the data folder.
            var settings = new JsonSettingsStore(paths.SettingsFile);
            var language = new LanguageService(Localizer, settings);
            language.Initialize();

            var window = new MainWindow();
            GuardAgainstFailures(new ErrorLog(paths.ErrorLogFile), new AvaloniaDialogService(() => window));
            window.DataContext = Compose(paths, settings, language, () => window);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                (window.DataContext as MainViewModel)?.Generation.Dispose();
                _http?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Keeps the window open when an action fails in a way nobody expected: the failure is written to the log of the
    /// data folder and shown to the user, one notice at a time, instead of ending the application. A failure of a task
    /// nobody waited for is written to the log too.
    /// </summary>
    /// <param name="log">The log of the failures.</param>
    /// <param name="dialogs">The dialogs that show the notice.</param>
    private void GuardAgainstFailures(IErrorLog log, IDialogService dialogs)
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write(e.Exception);
            e.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            var written = log.Write(e.Exception);
            if (_showingFailure)
            {
                return;
            }

            _showingFailure = true;
            var notice = new ConfirmDialogViewModel(
                Localizer.Get("Failure.Title"),
                written
                    ? Localizer.Format("Failure.MessageLogged", Localizer.Describe(e.Exception), log.FilePath)
                    : Localizer.Format("Failure.Message", Localizer.Describe(e.Exception)),
                Localizer.Get("Common.Close"),
                IsNotice: true);
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await dialogs.ConfirmAsync(notice);
                }
                finally
                {
                    _showingFailure = false;
                }
            });
        };
    }

    /// <summary>
    /// Creates the services and the view models of the window.
    /// </summary>
    /// <param name="paths">The data folders.</param>
    /// <param name="preferences">The settings of the user.</param>
    /// <param name="language">The language service, already initialized.</param>
    /// <param name="owner">Returns the window that owns the dialogs.</param>
    /// <returns>The view model of the main window.</returns>
    private MainViewModel Compose(AppPaths paths, ISettingsStore preferences, ILanguageService language, Func<Window?> owner)
    {
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Voicer", typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0"));

        var dialogs = new AvaloniaDialogService(owner);
        var downloader = new FileDownloader(_http);
        var store = new ModelStore(paths.ModelsDirectory);
        var ffmpeg = new FfmpegLocator(paths.ToolsDirectory);
        var worker = new ProcessWorkerClient(WorkerLaunch.ForCurrentProcess());
        var runner = new TranscriptionRunner(new FfmpegAudioExtractor(ffmpeg), worker, paths.TempDirectory);
        runner.ClearLeftovers();
        var history = new SqliteTranscriptHistory(paths.DatabaseFile);
        var temporaryAudio = new TemporaryAudioFolder(Path.Combine(paths.TempDirectory, "Audio"));
        temporaryAudio.ClearLeftovers();
        var keeper = new RecordKeeper(history, new FfmpegAudioEncoder(ffmpeg), temporaryAudio);
        var decoder = new FfmpegPcmDecoder(ffmpeg);
        Func<string, TimeSpan, IAudioPlayer> createPlayer =
            (file, duration) => new AudioPlayer(file, duration, decoder, () => new OpenAlOutput());
        var voiceStore = new VoiceModelStore(paths.ModelsDirectory);
        var llamaCpp = new LlamaCppLocator(paths.ToolsDirectory);
        var generationRunner = new GenerationRunner(
            new LlamaTtsSynthesizer(llamaCpp, voiceStore),
            new FfmpegVoiceSampleExtractor(ffmpeg),
            Path.Combine(paths.TempDirectory, "Generation"));
        generationRunner.ClearLeftovers();
        var languageModels = new OpenAiCompatibleClient(_http);
        var summaryPreferences = new SummaryPreferencesStore(preferences);
        var summaries = new RecordSummaryService(new Summarizer(languageModels, Localizer), summaryPreferences, history);
        var recordSearch = new RecordSearch(new SqliteSearchIndex(paths.DatabaseFile), languageModels);
        var search = new RecordSearchService(recordSearch, new RecordQuestions(recordSearch, languageModels, Localizer), summaryPreferences);
        var viewer = new RecordViewer(
            Localizer,
            history,
            dialogs,
            temporaryAudio,
            createPlayer,
            summaries);

        var main = new MainViewModel();
        var transcription = new TranscriptionViewModel(
            Localizer,
            store,
            worker,
            runner,
            dialogs,
            keeper,
            viewer,
            summaries,
            ffmpeg,
            new SourceFileInspector(),
            new SpeechDetectorSource(store, new ModelInstaller(downloader, store)),
            () => main.SelectTab(TabKey.Settings));
        var generation = new GenerationViewModel(
            Localizer,
            voiceStore,
            worker,
            generationRunner,
            dialogs,
            keeper,
            ffmpeg,
            llamaCpp,
            new GenerationPreferencesStore(preferences),
            createPlayer,
            () => main.SelectTab(TabKey.Settings));
        var settings = new SettingsViewModel(
            Localizer,
            new LanguageViewModel(Localizer, language, FlagImages.Load),
            new ModelCatalogViewModel(
                Localizer,
                "Settings.Models",
                "Models.SpeechWindowHint",
                [.. SpeechModelCatalog.All
                    .GroupBy(m => m.Family)
                    .Select(family => new ModelItemViewModel(Localizer, [.. family], store, new ModelInstaller(downloader, store), dialogs))],
                dialogs),
            new ModelCatalogViewModel(
                Localizer,
                "Settings.VoiceModels",
                "Models.VoiceWindowHint",
                [.. VoiceModelCatalog.All
                    .Select(model => new VoiceModelItemViewModel(Localizer, model, voiceStore, new VoiceModelInstaller(downloader, voiceStore), dialogs))],
                dialogs),
            new FfmpegItemViewModel(Localizer, ffmpeg, new FfmpegInstaller(downloader, ffmpeg)),
            new LlamaCppItemViewModel(Localizer, llamaCpp, new LlamaCppInstaller(downloader, llamaCpp, LlamaCppPackage.ForCurrentSystem())),
            new SummarySettingsViewModel(Localizer, summaryPreferences, languageModels),
            paths.DataDirectory,
            dialogs);
        main.Attach(transcription, generation, new HistoryViewModel(Localizer, history, viewer, dialogs, search), settings);

        return main;
    }
}
