using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Devices;
using Voicer.Core.Media;
using Voicer.Core.Playback;
using Voicer.Core.Synthesis;
using Voicer.Localization;
using Voicer.Media.Ffmpeg;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Devices;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.Generation;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Formatting;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// The page that turns a text into speech: a model, its language and voice, the text, then the generation, its player
/// and the saving of the recording. The model leaves memory as soon as the generation ends; the result goes into the
/// history marked as a generation.
/// </summary>
public sealed partial class GenerationViewModel : PageViewModelBase, IDisposable
{
    /// <summary>
    /// The words a narrator speaks in a minute, for the estimate of the length of the speech.
    /// </summary>
    private const double WORDS_PER_MINUTE = 150;

    /// <summary>
    /// The store that knows the installed voice models.
    /// </summary>
    private readonly IVoiceModelStore _store;

    /// <summary>
    /// The worker that lists the devices.
    /// </summary>
    private readonly IWorkerClient _worker;

    /// <summary>
    /// The runner of the generation.
    /// </summary>
    private readonly IGenerationRunner _runner;

    /// <summary>
    /// The dialogs: the file pickers.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Keeps every generated speech in the history.
    /// </summary>
    private readonly IRecordKeeper _keeper;

    /// <summary>
    /// The locator of FFmpeg, which prepares a sample and compresses the speech for the history.
    /// </summary>
    private readonly IFfmpegLocator _ffmpeg;

    /// <summary>
    /// The locator of llama.cpp, which speaks.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp;

    /// <summary>
    /// Keeps the choices of the page.
    /// </summary>
    private readonly IGenerationPreferencesStore _preferences;

    /// <summary>
    /// Creates the player of a sound file of a known length.
    /// </summary>
    private readonly Func<string, TimeSpan, IAudioPlayer> _createPlayer;

    /// <summary>
    /// Opens the settings, where models and tools are installed.
    /// </summary>
    private readonly Action _openSettings;

    /// <summary>
    /// The clock that measures how long a generation took.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// The languages and voices of every model, made once, so switching models does not make them again.
    /// </summary>
    private readonly Dictionary<string, (IReadOnlyList<SpeechLanguageViewModel> Languages, IReadOnlyList<VoiceOptionViewModel> Voices)> _choices = [];

    /// <summary>
    /// The choices kept from the last generation.
    /// </summary>
    private GenerationPreferences _remembered;

    /// <summary>
    /// Stops the generation in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _generation;

    /// <summary>
    /// Whether the devices were asked for already.
    /// </summary>
    private bool _devicesRequested;

    /// <summary>
    /// How long the last generation took.
    /// </summary>
    private TimeSpan _elapsed;

    /// <summary>
    /// The text to speak.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText), nameof(TextStats))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _text = string.Empty;

    /// <summary>
    /// The chosen voice model, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseSample), nameof(ShowSampleHint))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private VoiceModel? _selectedModel;

    /// <summary>
    /// The chosen language, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private SpeechLanguageViewModel? _selectedLanguage;

    /// <summary>
    /// The chosen voice shipped with the model, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private VoiceOptionViewModel? _selectedVoice;

    /// <summary>
    /// The file with a sample of the voice, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSample), nameof(SampleName), nameof(ShowSampleHint))]
    [NotifyCanExecuteChangedFor(nameof(ClearSampleCommand))]
    private string? _samplePath;

    /// <summary>
    /// The chosen device, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private DeviceOptionViewModel? _selectedDevice;

    /// <summary>
    /// Whether the devices are being found out.
    /// </summary>
    [ObservableProperty]
    private bool _isDetectingDevices;

    /// <summary>
    /// Whether a generation runs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand), nameof(CancelCommand), nameof(BrowseSampleCommand), nameof(ClearSampleCommand), nameof(SaveCommand))]
    private bool _isBusy;

    /// <summary>
    /// The progress of the generation, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StageText), nameof(IsIndeterminate), nameof(ProgressPercent))]
    private GenerationProgress? _progress;

    /// <summary>
    /// How much of the history saving is done, from 0 to 100.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StageText), nameof(ProgressPercent))]
    private double _savingPercent;

    /// <summary>
    /// The result of the last generation, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ResultSummary), nameof(ShowsResultNotice))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private GenerationResult? _result;

    /// <summary>
    /// Whether the user closed the notice of the last result; a new result shows its notice again.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsResultNotice))]
    private bool _isResultNoticeClosed;

    /// <summary>
    /// The player of the result, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayer))]
    private PlayerViewModel? _player;

    /// <summary>
    /// Where the result was saved, in words, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveStatus))]
    private string? _saveStatus;

    /// <summary>
    /// The problem to show, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// Whether the problem is fixed in the settings (a missing model or tool).
    /// </summary>
    [ObservableProperty]
    private bool _canFixInSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">The store that knows the installed voice models.</param>
    /// <param name="worker">The worker that lists the devices.</param>
    /// <param name="runner">The runner of the generation.</param>
    /// <param name="dialogs">The dialogs: the file pickers.</param>
    /// <param name="keeper">Keeps every generated speech in the history.</param>
    /// <param name="ffmpeg">The locator of FFmpeg.</param>
    /// <param name="llamaCpp">The locator of llama.cpp.</param>
    /// <param name="preferences">Keeps the choices of the page.</param>
    /// <param name="createPlayer">Creates the player of a sound file of a known length.</param>
    /// <param name="openSettings">Opens the settings.</param>
    /// <param name="time">The clock that measures a generation; the system clock by default.</param>
    public GenerationViewModel(
        ILocalizer localizer,
        IVoiceModelStore store,
        IWorkerClient worker,
        IGenerationRunner runner,
        IDialogService dialogs,
        IRecordKeeper keeper,
        IFfmpegLocator ffmpeg,
        ILlamaCppLocator llamaCpp,
        IGenerationPreferencesStore preferences,
        Func<string, TimeSpan, IAudioPlayer> createPlayer,
        Action openSettings,
        TimeProvider? time = null)
        : base(localizer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _keeper = keeper ?? throw new ArgumentNullException(nameof(keeper));
        _ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _createPlayer = createPlayer ?? throw new ArgumentNullException(nameof(createPlayer));
        _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        _time = time ?? TimeProvider.System;

        _remembered = preferences.Load();
        _samplePath = File.Exists(_remembered.SamplePath) ? _remembered.SamplePath : null;
    }

    /// <summary>
    /// Gets the installed voice models.
    /// </summary>
    public ObservableCollection<VoiceModel> Models { get; } = [];

    /// <summary>
    /// Gets the languages the chosen model speaks.
    /// </summary>
    public ObservableCollection<SpeechLanguageViewModel> Languages { get; } = [];

    /// <summary>
    /// Gets the voices shipped with the chosen model.
    /// </summary>
    public ObservableCollection<VoiceOptionViewModel> Voices { get; } = [];

    /// <summary>
    /// Gets the devices a model can run on.
    /// </summary>
    public ObservableCollection<DeviceOptionViewModel> Devices { get; } = [];

    /// <summary>
    /// Gets a value indicating whether a voice model is installed.
    /// </summary>
    public bool HasModels => Models.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the chosen model speaks several languages, so one is chosen.
    /// </summary>
    public bool HasLanguageChoice => Languages.Count > 1;

    /// <summary>
    /// Gets a value indicating whether the chosen model ships voices to choose from.
    /// </summary>
    public bool HasVoices => Voices.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the chosen model speaks in the voice of a sample.
    /// </summary>
    public bool CanUseSample => SelectedModel?.CanCloneVoice == true;

    /// <summary>
    /// Gets a value indicating whether a sample of the voice is chosen.
    /// </summary>
    public bool HasSample => SamplePath is not null;

    /// <summary>
    /// Gets the name of the file with the sample.
    /// </summary>
    public string SampleName => SamplePath is null ? string.Empty : Path.GetFileName(SamplePath);

    /// <summary>
    /// Gets a value indicating whether the chosen model asks for a sample that is not chosen.
    /// </summary>
    public bool ShowSampleHint => SelectedModel?.NeedsSample == true && !HasSample;

    /// <summary>
    /// Gets a value indicating whether there is something to speak: a text that is not only spaces.
    /// </summary>
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    /// <summary>
    /// Gets how many words the text has and about how long they take to speak.
    /// </summary>
    public string TextStats
    {
        get
        {
            var words = Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var speech = TimeSpan.FromMinutes(words / WORDS_PER_MINUTE);

            return Localizer.Format("Generation.TextStats", words, DisplayText.Duration(speech));
        }
    }

    /// <summary>
    /// Gets a value indicating whether no generation runs.
    /// </summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// Gets a value indicating whether the progress of the stage cannot be measured.
    /// </summary>
    public bool IsIndeterminate => Progress?.Stage is GenerationStage.PreparingVoice or GenerationStage.Joining;

    /// <summary>
    /// Gets how much of the generation is done, from 0 to 100.
    /// </summary>
    public double ProgressPercent => Progress?.Stage == GenerationStage.SavingRecord ? SavingPercent : Progress?.Percent ?? 0;

    /// <summary>
    /// Gets what the generation is doing, in words.
    /// </summary>
    public string StageText => Progress switch
    {
        { Stage: GenerationStage.PreparingVoice } => Localizer.Get("Generation.Stage.PreparingVoice"),
        { Stage: GenerationStage.Speaking } p => Localizer.Format("Generation.Stage.Speaking", p.Part, p.Parts),
        { Stage: GenerationStage.Joining } => Localizer.Get("Generation.Stage.Joining"),
        { Stage: GenerationStage.SavingRecord } => Localizer.Format("Generation.Stage.SavingRecord", SavingPercent),
        _ => Localizer.Get("Generation.Stage.Starting"),
    };

    /// <summary>
    /// Gets a value indicating whether a result is there.
    /// </summary>
    public bool HasResult => Result is not null;

    /// <summary>
    /// Gets a value indicating whether the notice of the result is shown: there is a result and its notice is not closed.
    /// </summary>
    public bool ShowsResultNotice => HasResult && !IsResultNoticeClosed;

    /// <summary>
    /// Gets a value indicating whether the result can be listened to.
    /// </summary>
    public bool HasPlayer => Player is not null;

    /// <summary>
    /// Gets how the last result was made, in words.
    /// </summary>
    public string ResultSummary => Result is null
        ? string.Empty
        : Localizer.Format("Generation.ResultSummary", DisplayText.Duration(Result.Duration), DisplayText.Duration(_elapsed));

    /// <summary>
    /// Gets a value indicating whether the place of the saved result is shown.
    /// </summary>
    public bool HasSaveStatus => !string.IsNullOrEmpty(SaveStatus);

    /// <summary>
    /// Gets a value indicating whether a problem is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <inheritdoc />
    public override void Activate()
    {
        RefreshModels();
        if (!_devicesRequested)
        {
            _devicesRequested = true;
            _ = LoadDevicesAsync();
        }
    }

    /// <summary>
    /// Brings the list of installed models up to date, keeping the chosen one when it is still there.
    /// </summary>
    public void RefreshModels()
    {
        var chosen = SelectedModel?.Id ?? _remembered.ModelId;
        Models.Clear();
        foreach (var model in _store.Installed())
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault(m => m.Id == chosen) ?? Models.FirstOrDefault();
        OnPropertyChanged(nameof(HasModels));
    }

    /// <summary>
    /// Asks the worker for the devices; when it cannot answer, the processor is offered alone.
    /// </summary>
    /// <returns>A task that completes when the list is filled.</returns>
    public async Task LoadDevicesAsync()
    {
        IsDetectingDevices = true;
        IReadOnlyList<ComputeDevice> devices;
        try
        {
            devices = await DeviceDetection.ListAsync(_worker, CancellationToken.None);
        }
        finally
        {
            IsDetectingDevices = false;
        }

        Devices.Clear();
        foreach (var device in devices)
        {
            Devices.Add(new DeviceOptionViewModel(Localizer, device));
        }

        SelectedDevice = DeviceDetection.Preferred(Devices);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _generation?.Cancel();
        Player?.Dispose();
    }

    /// <summary>
    /// Offers the languages and voices of the newly chosen model, keeping the remembered ones when it has them.
    /// </summary>
    /// <param name="value">The chosen model.</param>
    partial void OnSelectedModelChanged(VoiceModel? value)
    {
        var language = SelectedLanguage?.Code ?? _remembered.Language;
        var voice = SelectedVoice?.Id ?? _remembered.Voice;

        Languages.Clear();
        Voices.Clear();
        if (value is not null)
        {
            var (languages, voices) = ChoicesOf(value);
            foreach (var item in languages)
            {
                Languages.Add(item);
            }

            foreach (var item in voices)
            {
                Voices.Add(item);
            }
        }

        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == language) ?? Languages.FirstOrDefault();
        SelectedVoice = Voices.FirstOrDefault(v => v.Id == voice) ?? Voices.FirstOrDefault();
        OnPropertyChanged(nameof(HasLanguageChoice));
        OnPropertyChanged(nameof(HasVoices));
    }

    /// <summary>
    /// Speaks the text with the chosen model, voice and device, keeps the result in the history and opens its player.
    /// </summary>
    /// <returns>A task that completes when the generation ends.</returns>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        ErrorMessage = null;
        SaveStatus = null;
        if (!CheckTools())
        {
            return;
        }

        var model = SelectedModel!;
        var device = SelectedDevice!;
        var sample = CanUseSample ? SamplePath : null;
        var voice = new SynthesisVoice(model, SelectedLanguage!.Code, SelectedVoice?.Id, sample, device.Device);
        var request = new GenerationRequest(Text, voice, device.Title);
        Remember(voice);

        ClearResult();
        Progress = null;
        IsBusy = true;
        _generation = new CancellationTokenSource();
        var started = _time.GetTimestamp();
        try
        {
            var progress = new Progress<GenerationProgress>(p =>
            {
                if (IsBusy)
                {
                    Progress = p;
                }
            });
            var result = await _runner.RunAsync(request, progress, _generation.Token);
            _elapsed = _time.GetElapsedTime(started);
            Result = result;
            Player = new PlayerViewModel(Localizer, _createPlayer(result.AudioPath, result.Duration));
            await KeepAsync(result, _generation.Token);
        }
        catch (OperationCanceledException)
        {
            // The user stopped it: nothing to report.
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or Win32Exception)
        {
            ShowError(Localizer.Format("Generation.Failed", Localizer.Describe(ex)), false);
        }
        finally
        {
            _generation.Dispose();
            _generation = null;
            IsBusy = false;
            Progress = null;
        }
    }

    /// <summary>
    /// Tells whether the generation can start: a text that is not only spaces, a model, its language and voice, a
    /// device, and nothing running.
    /// </summary>
    /// <returns><see langword="true"/> when everything is chosen.</returns>
    private bool CanGenerate()
    {
        return HasText && SelectedModel is not null && SelectedLanguage is not null && SelectedDevice is not null &&
            (Voices.Count == 0 || SelectedVoice is not null) && !IsBusy;
    }

    /// <summary>
    /// Stops the generation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _generation?.Cancel();
    }

    /// <summary>
    /// Picks a file with a sample of the voice: any audio or video file; its first thirty seconds are taken.
    /// </summary>
    /// <returns>A task that completes when the picker is closed.</returns>
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BrowseSampleAsync()
    {
        var path = await _dialogs.PickMediaFileAsync(
            Localizer.Get("Generation.SamplePickerTitle"),
            Localizer.Get("Transcription.PickerFilter"),
            MediaFormats.AllExtensions);
        if (path is null)
        {
            return;
        }

        if (!MediaFormats.IsSupported(path))
        {
            ShowError(Localizer.Format("Transcription.Unsupported", Path.GetFileName(path)), false);

            return;
        }

        ErrorMessage = null;
        SamplePath = Path.GetFullPath(path);
    }

    /// <summary>
    /// Forgets the sample: the model speaks in its own voice.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearSample))]
    private void ClearSample()
    {
        SamplePath = null;
    }

    /// <summary>
    /// Tells whether the sample can be forgotten.
    /// </summary>
    /// <returns><see langword="true"/> when a sample is chosen and nothing runs.</returns>
    private bool CanClearSample()
    {
        return HasSample && !IsBusy;
    }

    /// <summary>
    /// Saves the recording of the result where the user chooses, as a WAV file.
    /// </summary>
    /// <returns>A task that completes when the file is written or the picker is closed.</returns>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var result = Result!;
        var path = await _dialogs.PickSaveFileAsync(
            Localizer.Get("Generation.SaveTitle"),
            Path.GetFileName(result.AudioPath),
            Localizer.Get("Generation.SaveFilter"),
            GeneratedFileName.EXTENSION.TrimStart('.'));
        if (path is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => File.Copy(result.AudioPath, path, overwrite: true));
            SaveStatus = Localizer.Format("Generation.Saved", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(Localizer.Format("Generation.SaveFailed", Localizer.Describe(ex)), false);
        }
    }

    /// <summary>
    /// Tells whether the result can be saved now.
    /// </summary>
    /// <returns><see langword="true"/> when there is a result and nothing runs.</returns>
    private bool CanSave()
    {
        return HasResult && !IsBusy;
    }

    /// <summary>
    /// Opens the settings, where a missing model or tool is installed.
    /// </summary>
    [RelayCommand]
    private void OpenSettings()
    {
        _openSettings();
    }

    /// <summary>
    /// Shows the notice of every new result, even after the previous one was closed.
    /// </summary>
    /// <param name="value">The new result.</param>
    partial void OnResultChanged(GenerationResult? value)
    {
        IsResultNoticeClosed = false;
    }

    /// <summary>
    /// Closes the notice of the result; the result itself stays.
    /// </summary>
    [RelayCommand]
    private void CloseResultNotice()
    {
        IsResultNoticeClosed = true;
    }

    /// <summary>
    /// Hides the problem.
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
    }

    /// <summary>
    /// Checks that the engine, the model and, for a sample, FFmpeg are there.
    /// </summary>
    /// <returns><see langword="true"/> when the generation can run.</returns>
    private bool CheckTools()
    {
        if (_llamaCpp.Locate() is null)
        {
            ShowError(Localizer.Get("Generation.EngineMissing"), true);

            return false;
        }

        if (!_store.IsInstalled(SelectedModel!))
        {
            RefreshModels();
            ShowError(Localizer.Get("Generation.ModelGone"), true);

            return false;
        }

        if (CanUseSample && HasSample && _ffmpeg.Locate() is null)
        {
            ShowError(Localizer.Get("Transcription.FfmpegMissing"), true);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Keeps a generated speech in the history with its sound; a failure is shown, the result stays on the page.
    /// </summary>
    /// <param name="result">The generated speech.</param>
    /// <param name="cancellationToken">Stops the compression of the sound; the text is kept anyway.</param>
    /// <returns>A task that completes when the record is saved or the failure is shown.</returns>
    private async Task KeepAsync(GenerationResult result, CancellationToken cancellationToken)
    {
        SavingPercent = 0;
        Progress = new GenerationProgress(GenerationStage.SavingRecord, 0, 0);
        var progress = new Progress<double>(p =>
        {
            if (IsBusy)
            {
                SavingPercent = p;
            }
        });

        try
        {
            var kept = await _keeper.KeepAsync(result.Transcript, progress, cancellationToken);
            if (kept.AudioFailure is { } failure)
            {
                ShowError(Localizer.Format("Generation.AudioNotKept", Localizer.Describe(failure)), false);
            }
        }
        catch (IOException ex)
        {
            ShowError(Localizer.Format("Transcription.SaveFailed", Localizer.Describe(ex)), false);
        }
    }

    /// <summary>
    /// Closes the player of the previous result and forgets it: its file goes with the next generation.
    /// </summary>
    private void ClearResult()
    {
        var player = Player;
        Player = null;
        player?.Dispose();
        Result = null;
    }

    /// <summary>
    /// Keeps the choices of this generation for the next start.
    /// </summary>
    /// <param name="voice">How the text is spoken.</param>
    private void Remember(SynthesisVoice voice)
    {
        _remembered = new GenerationPreferences(voice.Model.Id, voice.Language, voice.Voice, SamplePath);
        try
        {
            _preferences.Save(_remembered);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not remembered: the generation runs anyway, the choices are made again next time.
        }
    }

    /// <summary>
    /// Gets the languages and voices of a model, made once.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>Its languages and voices.</returns>
    private (IReadOnlyList<SpeechLanguageViewModel> Languages, IReadOnlyList<VoiceOptionViewModel> Voices) ChoicesOf(VoiceModel model)
    {
        if (!_choices.TryGetValue(model.Id, out var choices))
        {
            choices = (
                [.. model.Languages.Select(code => new SpeechLanguageViewModel(Localizer, code))],
                [.. model.Voices.Select(id => new VoiceOptionViewModel(Localizer, id))]);
            _choices[model.Id] = choices;
        }

        return choices;
    }

    /// <summary>
    /// Shows a problem.
    /// </summary>
    /// <param name="message">The problem.</param>
    /// <param name="canFixInSettings">Whether the settings fix it.</param>
    private void ShowError(string message, bool canFixInSettings)
    {
        CanFixInSettings = canFixInSettings;
        ErrorMessage = message;
    }
}
