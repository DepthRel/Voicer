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
    /// The file whose speech is recognized, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSource), nameof(SourceName), nameof(SourceDetails))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand), nameof(ClearSourceCommand))]
    private string? _sourcePath;

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
    [NotifyPropertyChangedFor(nameof(IsIdle))]
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
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ResultSummary))]
    [NotifyCanExecuteChangedFor(nameof(ShowResultCommand), nameof(SummarizeCommand))]
    private Transcript? _result;

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
        Action openSettings,
        TimeProvider? time = null)
        : base(localizer)
    {
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
    public bool HasSource => SourcePath is not null;

    /// <summary>
    /// Gets the name of the chosen file.
    /// </summary>
    public string SourceName => SourcePath is null ? string.Empty : Path.GetFileName(SourcePath);

    /// <summary>
    /// Gets the size and the folder of the chosen file.
    /// </summary>
    public string SourceDetails
    {
        get
        {
            if (SourcePath is null)
            {
                return string.Empty;
            }

            var file = new FileInfo(SourcePath);
            var size = file.Exists ? DisplayText.Bytes(Localizer, file.Length) : string.Empty;

            return Localizer.Format("Transcription.SourceDetails", size, file.DirectoryName ?? string.Empty);
        }
    }

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
    public bool IsIndeterminate => Stage == TranscriptionStage.LoadingModel;

    /// <summary>
    /// Gets what the recognition is doing, in words.
    /// </summary>
    public string StageText => Stage switch
    {
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
    /// Takes a file to recognize, when its kind can hold sound.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when the file was taken.</returns>
    public bool TrySetSource(string? path)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!MediaFormats.IsSupported(path))
        {
            ShowError(Localizer.Format("Transcription.Unsupported", Path.GetFileName(path)), false);

            return false;
        }

        ErrorMessage = null;
        SourcePath = Path.GetFullPath(path);
        Result = null;

        return true;
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
    [RelayCommand(CanExecute = nameof(IsIdle))]
    private async Task BrowseAsync()
    {
        var path = await _dialogs.PickMediaFileAsync(
            Localizer.Get("Transcription.PickerTitle"),
            Localizer.Get("Transcription.PickerFilter"),
            MediaFormats.AllExtensions);
        TrySetSource(path);
    }

    /// <summary>
    /// Forgets the chosen file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanClearSource))]
    private void ClearSource()
    {
        SourcePath = null;
        Result = null;
    }

    /// <summary>
    /// Tells whether the chosen file can be forgotten.
    /// </summary>
    /// <returns><see langword="true"/> when a file is chosen and no recognition runs.</returns>
    private bool CanClearSource()
    {
        return HasSource && !IsBusy;
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
        var request = new TranscriptionRequest(SourcePath!, SelectedModel!, _store.FileOf(SelectedModel!), device.Device, device.Title);
        var started = _time.GetTimestamp();

        Result = null;
        _recordId = null;
        Progress = 0;
        IsBusy = true;
        _recognition = new CancellationTokenSource();
        try
        {
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
    /// Tells whether the recognition can start: a file, a model and a device are chosen and nothing runs.
    /// </summary>
    /// <returns><see langword="true"/> when everything is chosen.</returns>
    private bool CanRecognize()
    {
        return HasSource && SelectedModel is not null && SelectedDevice is not null && !IsBusy;
    }

    /// <summary>
    /// Stops the recognition.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _recognition?.Cancel();
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
    /// Writes the summary of the recognized text with the model of the settings, keeps it in the history and opens the
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
        var transcript = Result!;

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
            await _summaries.SummarizeAsync(id, transcript, progress, _recognition.Token);
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
