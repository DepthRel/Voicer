using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Devices;
using Voicer.Core.Media;
using Voicer.Core.Models;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.Media.Ffmpeg;
using Voicer.UI.Enums;
using Voicer.UI.Services.Devices;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Summaries;
using Voicer.UI.Services.Transcription;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Formatting;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// The page that turns speech into text: a file, a model and a device, then the recognition and its result.
/// </summary>
public sealed partial class TranscriptionViewModel : PageViewModelBase
{
    /// <summary>
    /// The store that knows the installed models.
    /// </summary>
    private readonly IModelStore _store;

    /// <summary>
    /// The worker that lists the devices.
    /// </summary>
    private readonly IWorkerClient _worker;

    /// <summary>
    /// The runner of the recognition.
    /// </summary>
    private readonly ITranscriptionRunner _runner;

    /// <summary>
    /// The dialogs: the file picker.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// The locator of FFmpeg, which the recognition needs.
    /// </summary>
    private readonly IFfmpegLocator _ffmpeg;

    /// <summary>
    /// Keeps every recognized text in the history, with its sound.
    /// </summary>
    private readonly IRecordKeeper _keeper;

    /// <summary>
    /// Opens the window of a recognized text.
    /// </summary>
    private readonly IRecordViewer _viewer;

    /// <summary>
    /// Writes and keeps the summaries.
    /// </summary>
    private readonly IRecordSummaryService _summaries;

    /// <summary>
    /// Checks a chosen file before the page takes it.
    /// </summary>
    private readonly ISourceFileInspector _inspector;

    /// <summary>
    /// Gives the recognition the detector of speech, downloading it once.
    /// </summary>
    private readonly ISpeechDetectorSource _detector;

    /// <summary>
    /// The clock that measures how long a recognition took.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Opens the settings, where models and FFmpeg are installed.
    /// </summary>
    private readonly Action _openSettings;

    /// <summary>
    /// Stops the recognition in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _recognition;

    /// <summary>
    /// Whether the devices were asked for already.
    /// </summary>
    private bool _devicesRequested;

    /// <summary>
    /// How long the last recognition took.
    /// </summary>
    private TimeSpan _elapsed;

    /// <summary>
    /// The record of the last result in the history, or <see langword="null"/> when it could not be saved.
    /// </summary>
    private long? _recordId;

    /// <summary>
    /// The request of the summary being made, from 1.
    /// </summary>
    private int _summaryStep;

    /// <summary>
    /// How many requests the summary being made takes.
    /// </summary>
    private int _summarySteps;

    /// <summary>
    /// The checked file whose speech is recognized, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource), nameof(SourcePath), nameof(SourceName), nameof(SourceDetails))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand), nameof(ClearSourceCommand))]
    private SourceFile? _source;

    /// <summary>
    /// Whether a chosen file is being checked.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTakeSource))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand), nameof(BrowseCommand), nameof(ClearSourceCommand))]
    private bool _isLoadingSource;

    /// <summary>
    /// The chosen model, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    private SpeechModel? _selectedModel;

    /// <summary>
    /// The chosen device, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    private DeviceOptionViewModel? _selectedDevice;

    /// <summary>
    /// Whether the devices are being found out.
    /// </summary>
    [ObservableProperty]
    private bool _isDetectingDevices;

    /// <summary>
    /// Whether a recognition runs.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle), nameof(CanTakeSource))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand), nameof(CancelCommand), nameof(BrowseCommand), nameof(ClearSourceCommand), nameof(SummarizeCommand))]
    private bool _isBusy;

    /// <summary>
    /// What the recognition is doing, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StageText), nameof(IsIndeterminate))]
    private TranscriptionStage? _stage;

    /// <summary>
    /// How much of the current stage is done, from 0 to 100.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StageText))]
    private double _progress;

    /// <summary>
    /// The result of the last recognition, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ResultSummary), nameof(ShowsResultNotice))]
    [NotifyCanExecuteChangedFor(nameof(ShowResultCommand), nameof(SummarizeCommand))]
    private Transcript? _result;

    /// <summary>
    /// Whether the user closed the notice of the last result; a new result shows its notice again.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsResultNotice))]
    private bool _isResultNoticeClosed;

    /// <summary>
    /// The problem to show, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>
    /// Whether the problem is fixed in the settings (a missing model or FFmpeg).
    /// </summary>
    [ObservableProperty]
    private bool _canFixInSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="store">The store that knows the installed models.</param>
    /// <param name="worker">The worker that lists the devices.</param>
    /// <param name="runner">The runner of the recognition.</param>
    /// <param name="dialogs">The dialogs: the file picker.</param>
    /// <param name="keeper">Keeps every recognized text in the history, with its sound.</param>
    /// <param name="viewer">Opens the window of a recognized text.</param>
    /// <param name="summaries">Writes and keeps the summaries.</param>
    /// <param name="ffmpeg">The locator of FFmpeg.</param>
    /// <param name="inspector">Checks a chosen file before the page takes it.</param>
    /// <param name="detector">Gives the recognition the detector of speech, downloading it once.</param>
    /// <param name="openSettings">Opens the settings.</param>
    /// <param name="time">The clock that measures a recognition; the system clock by default.</param>
    public TranscriptionViewModel(
        ILocalizer localizer,
        IModelStore store,
        IWorkerClient worker,
        ITranscriptionRunner runner,
        IDialogService dialogs,
        IRecordKeeper keeper,
        IRecordViewer viewer,
        IRecordSummaryService summaries,
        IFfmpegLocator ffmpeg,
        ISourceFileInspector inspector,
        ISpeechDetectorSource detector,
        Action openSettings,
        TimeProvider? time = null)
        : base(localizer)
    {
        _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _keeper = keeper ?? throw new ArgumentNullException(nameof(keeper));
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
        _openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the installed models.
    /// </summary>
    public ObservableCollection<SpeechModel> Models { get; } = [];

    /// <summary>
    /// Gets the devices a model can run on.
    /// </summary>
    public ObservableCollection<DeviceOptionViewModel> Devices { get; } = [];

    /// <summary>
    /// Gets a value indicating whether a file is chosen.
    /// </summary>
    public bool HasSource => Source is not null;

    /// <summary>
    /// Gets the full path of the chosen file, or <see langword="null"/>.
    /// </summary>
    public string? SourcePath => Source?.FullPath;

    /// <summary>
    /// Gets the name of the chosen file.
    /// </summary>
    public string SourceName => Source is null ? string.Empty : Path.GetFileName(Source.FullPath);

    /// <summary>
    /// Gets the size and the folder of the chosen file, as they were when it was checked.
    /// </summary>
    public string SourceDetails => Source is null
        ? string.Empty
        : Localizer.Format(
            "Transcription.SourceDetails",
            DisplayText.Bytes(Localizer, Source.SizeBytes),
            Path.GetDirectoryName(Source.FullPath) ?? string.Empty);

    /// <summary>
    /// Gets a value indicating whether a file can be chosen now: nothing runs and no file is being checked.
    /// </summary>
    public bool CanTakeSource => !IsBusy && !IsLoadingSource;

    /// <summary>
    /// Gets a value indicating whether a model is installed.
    /// </summary>
    public bool HasModels => Models.Count > 0;

    /// <summary>
    /// Gets a value indicating whether no recognition runs.
    /// </summary>
    public bool IsIdle => !IsBusy;

    /// <summary>
    /// Gets a value indicating whether the progress of the stage cannot be measured (the model is loading).
    /// </summary>
    public bool IsIndeterminate => Stage is TranscriptionStage.LoadingModel or TranscriptionStage.PreparingDetector;

    /// <summary>
    /// Gets what the recognition is doing, in words.
    /// </summary>
    public string StageText => Stage switch
    {
        TranscriptionStage.PreparingDetector => Localizer.Get("Transcription.Stage.PreparingDetector"),
        TranscriptionStage.ExtractingAudio => Localizer.Format("Transcription.Stage.ExtractingAudio", Progress),
        TranscriptionStage.LoadingModel => Localizer.Get("Transcription.Stage.LoadingModel"),
        TranscriptionStage.Recognizing => Localizer.Format("Transcription.Stage.Recognizing", Progress),
        TranscriptionStage.SavingRecord => Localizer.Format("Transcription.Stage.SavingRecord", Progress),
        TranscriptionStage.Summarizing => Localizer.Format("Transcription.Stage.Summarizing", _summaryStep, _summarySteps),
        _ => string.Empty,
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
    /// Gets how the last result was made, in words.
    /// </summary>
    public string ResultSummary
    {
        get
        {
            if (Result is null)
            {
                return string.Empty;
            }

            return Localizer.Format(
                "Transcription.ResultSummary",
                Result.Segments.Count,
                DisplayText.Duration(_elapsed),
                DisplayText.Language(Localizer, Result.Language));
        }
    }

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
    /// Takes a file to recognize, when its kind can hold sound and it can be read; while it is checked the drop area
    /// shows it, and a file that cannot be taken is reported instead of breaking the page.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when the file was taken.</returns>
    public async Task<bool> SetSourceAsync(string? path)
    {
        if (!CanTakeSource || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var name = Path.GetFileName(path);
        if (!MediaFormats.IsSupported(path))
        {
            ShowError(Localizer.Format("Transcription.Unsupported", name), false);

            return false;
        }

        ErrorMessage = null;
        IsLoadingSource = true;
        try
        {
            var file = await _inspector.InspectAsync(path, CancellationToken.None);
            Result = null;
            Source = file;

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShowError(Localizer.Format("Transcription.SourceFailed", name, Localizer.Describe(ex)), false);

            return false;
        }
        finally
        {
            IsLoadingSource = false;
        }
    }

    /// <summary>
    /// Brings the list of installed models up to date, keeping the chosen one when it is still there.
    /// </summary>
    public void RefreshModels()
    {
        var chosen = SelectedModel;
        Models.Clear();
        foreach (var model in _store.Installed())
        {
            Models.Add(model);
        }

        SelectedModel = Models.FirstOrDefault(m => m == chosen) ?? Models.FirstOrDefault();
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

    /// <summary>
    /// Picks a file to recognize.
    /// </summary>
    /// <returns>A task that completes when the picker is closed.</returns>
    [RelayCommand(CanExecute = nameof(CanTakeSource))]
    private async Task BrowseAsync()
    {
        var path = await _dialogs.PickMediaFileAsync(
            Localizer.Get("Transcription.PickerTitle"),
            Localizer.Get("Transcription.PickerFilter"),
            MediaFormats.AllExtensions);
        await SetSourceAsync(path);
    }

    /// <summary>
    /// Forgets the chosen file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearSource))]
    private void ClearSource()
    {
        Source = null;
        Result = null;
    }

    /// <summary>
    /// Tells whether the chosen file can be forgotten.
    /// </summary>
    /// <returns><see langword="true"/> when a file is chosen, it is not being checked and no recognition runs.</returns>
    private bool CanClearSource()
    {
        return HasSource && CanTakeSource;
    }

    /// <summary>
    /// Recognizes the speech of the chosen file.
    /// </summary>
    /// <returns>A task that completes when the recognition ends.</returns>
    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeAsync()
    {
        ErrorMessage = null;
        if (_ffmpeg.Locate() is null)
        {
            ShowError(Localizer.Get("Transcription.FfmpegMissing"), true);

            return;
        }

        if (!_store.IsInstalled(SelectedModel!))
        {
            RefreshModels();
            ShowError(Localizer.Get("Transcription.ModelMissing"), true);

            return;
        }

        var device = SelectedDevice!;
        var started = _time.GetTimestamp();

        Result = null;
        _recordId = null;
        Progress = 0;
        IsBusy = true;
        _recognition = new CancellationTokenSource();
        try
        {
            var (detector, detectorFailure) = await PrepareDetectorAsync(_recognition.Token);
            var request = new TranscriptionRequest(SourcePath!, SelectedModel!, _store.FileOf(SelectedModel!), device.Device, device.Title, detector);
            var progress = new Progress<TranscriptionProgress>(p =>
            {
                if (IsBusy)
                {
                    Stage = p.Stage;
                    Progress = p.Percent;
                }
            });

            var transcript = await _runner.RunAsync(request, progress, _recognition.Token);
            _elapsed = _time.GetElapsedTime(started);
            Result = transcript;
            if (detectorFailure is not null)
            {
                ShowError(Localizer.Format("Transcription.DetectorMissing", Localizer.Describe(detectorFailure)), false);
            }

            await SaveAsync(transcript, _recognition.Token);
        }
        catch (OperationCanceledException)
        {
            // The user stopped it: nothing to report.
        }
        catch (Exception ex) when (ex is WorkerException or IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowError(Localizer.Describe(ex), false);
        }
        finally
        {
            _recognition.Dispose();
            _recognition = null;
            IsBusy = false;
            Stage = null;
        }
    }

    /// <summary>
    /// Makes the detector of speech ready, downloading it once when it is missing; a detector that cannot be had does
    /// not stop the recognition, which then hears the whole sound.
    /// </summary>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>The file of the detector, or <see langword="null"/> with the reason it could not be had.</returns>
    private async Task<(string? File, Exception? Failure)> PrepareDetectorAsync(CancellationToken cancellationToken)
    {
        if (!_detector.IsReady)
        {
            Stage = TranscriptionStage.PreparingDetector;
        }

        try
        {
            return (await _detector.PrepareAsync(cancellationToken), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return (null, ex);
        }
    }

    /// <summary>
    /// Tells whether the recognition can start: a file, a model and a device are chosen and nothing runs.
    /// </summary>
    /// <returns><see langword="true"/> when everything is chosen, no file is being checked and nothing runs.</returns>
    private bool CanRecognize()
    {
        return HasSource && SelectedModel is not null && SelectedDevice is not null && CanTakeSource;
    }

    /// <summary>
    /// Stops the recognition or the summary once the user confirms it: an hour of speech is not thrown away by a slip
    /// of the mouse. Work that ended while the question was open is kept.
    /// </summary>
    /// <returns>A task that completes when the question is answered.</returns>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private async Task CancelAsync()
    {
        var summary = Stage == TranscriptionStage.Summarizing;
        var question = new ConfirmDialogViewModel(
            Localizer.Get(summary ? "Transcription.StopSummaryTitle" : "Transcription.StopTitle"),
            Localizer.Get(summary ? "Transcription.StopSummaryMessage" : "Transcription.StopMessage"),
            Localizer.Get("Transcription.StopConfirm"));
        if (await _dialogs.ConfirmAsync(question))
        {
            _recognition?.Cancel();
        }
    }

    /// <summary>
    /// Shows the recognized text in the window of its record, read from the history; a text that could not be saved is
    /// shown as it is.
    /// </summary>
    /// <returns>A task that completes when the window is closed.</returns>
    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShowResultAsync()
    {
        if (_recordId is { } id)
        {
            try
            {
                if (await _viewer.OpenAsync(id, CancellationToken.None))
                {
                    return;
                }
            }
            catch (IOException ex)
            {
                ShowError(Localizer.Format("History.OpenFailed", Localizer.Describe(ex)), false);

                return;
            }
        }

        // Not saved, or deleted from the history meanwhile: the text of this session is still here.
        await _viewer.ShowAsync(Result!);
    }

    /// <summary>
    /// Writes the summary of the recognized text, with the corrections the user made in the history, with the model of
    /// the settings, keeps it in the history and opens the
    /// window of the record with it. The speech engine is not in memory by then: its worker ended with the recognition.
    /// </summary>
    /// <returns>A task that completes when the window of the record is closed, or the summary failed or was stopped.</returns>
    [RelayCommand(CanExecute = nameof(CanSummarize))]
    private async Task SummarizeAsync()
    {
        ErrorMessage = null;
        if (!_summaries.IsConfigured)
        {
            ShowError(Localizer.Get("Summary.NotConfigured"), true);

            return;
        }

        var id = _recordId!.Value;

        _summaryStep = 1;
        _summarySteps = 1;
        Progress = 0;
        Stage = TranscriptionStage.Summarizing;
        IsBusy = true;
        _recognition = new CancellationTokenSource();
        var progress = new Progress<SummaryProgress>(p =>
        {
            if (IsBusy)
            {
                _summaryStep = p.Step;
                _summarySteps = p.Steps;
                Progress = 100.0 * (p.Step - 1) / p.Steps;
                OnPropertyChanged(nameof(StageText));
            }
        });

        var done = false;
        try
        {
            // The text as the history keeps it: the user may have corrected it in the window of the record meanwhile.
            await _summaries.SummarizeKeptAsync(id, progress, _recognition.Token);
            done = true;
        }
        catch (OperationCanceledException)
        {
            // The user stopped it: nothing to report.
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or InvalidOperationException)
        {
            ShowError(Localizer.Format("Transcription.SummaryFailed", Localizer.Describe(ex)), false);
        }
        finally
        {
            _recognition.Dispose();
            _recognition = null;
            IsBusy = false;
            Stage = null;
        }

        if (done)
        {
            await ShowResultAsync();
        }
    }

    /// <summary>
    /// Tells whether the recognized text can be summarized now.
    /// </summary>
    /// <returns><see langword="true"/> when the text is in the history and nothing else runs.</returns>
    private bool CanSummarize()
    {
        return HasResult && _recordId is not null && !IsBusy;
    }

    /// <summary>
    /// Keeps a recognized text in the history with its sound; a failure is shown, the result stays on the page.
    /// </summary>
    /// <param name="transcript">The recognized text.</param>
    /// <param name="cancellationToken">Stops the compression of the sound; the text is kept anyway.</param>
    /// <returns>A task that completes when the text is saved or the failure is shown.</returns>
    private async Task SaveAsync(Transcript transcript, CancellationToken cancellationToken)
    {
        Stage = TranscriptionStage.SavingRecord;
        Progress = 0;
        var progress = new Progress<double>(p =>
        {
            if (IsBusy)
            {
                Stage = TranscriptionStage.SavingRecord;
                Progress = p;
            }
        });

        try
        {
            var kept = await _keeper.KeepAsync(transcript, progress, cancellationToken);
            _recordId = kept.Id;
            if (kept.AudioFailure is { } failure)
            {
                ShowError(Localizer.Format("Transcription.AudioNotSaved", Localizer.Describe(failure)), false);
            }
        }
        catch (IOException ex)
        {
            ShowError(Localizer.Format("Transcription.SaveFailed", Localizer.Describe(ex)), false);
        }
    }

    /// <summary>
    /// Opens the settings, where a missing model or FFmpeg is installed.
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
    partial void OnResultChanged(Transcript? value)
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
