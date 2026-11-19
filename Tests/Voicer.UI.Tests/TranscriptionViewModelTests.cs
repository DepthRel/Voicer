using FluentAssertions;
using NSubstitute;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.History;
using Voicer.Core.Models;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Media.Ffmpeg;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Summaries;
using Voicer.UI.Services.Transcription;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the transcription page: when the recognition can start and which files are taken.
/// </summary>
public sealed class TranscriptionViewModelTests
{
    /// <summary>
    /// The store of the models.
    /// </summary>
    private readonly IModelStore _store = Substitute.For<IModelStore>();

    /// <summary>
    /// The worker.
    /// </summary>
    private readonly IWorkerClient _worker = Substitute.For<IWorkerClient>();

    /// <summary>
    /// The runner of the recognition.
    /// </summary>
    private readonly ITranscriptionRunner _runner = Substitute.For<ITranscriptionRunner>();

    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _ffmpeg = Substitute.For<IFfmpegLocator>();

    /// <summary>
    /// Keeps the recognized texts in the history.
    /// </summary>
    private readonly IRecordKeeper _keeper = Substitute.For<IRecordKeeper>();

    /// <summary>
    /// Opens the window of a record.
    /// </summary>
    private readonly IRecordViewer _viewer = Substitute.For<IRecordViewer>();

    /// <summary>
    /// Writes and keeps the summaries.
    /// </summary>
    private readonly IRecordSummaryService _summaries = Substitute.For<IRecordSummaryService>();

    /// <summary>
    /// The page under test.
    /// </summary>
    private readonly TranscriptionViewModel _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionViewModelTests"/> class.
    /// </summary>
    public TranscriptionViewModelTests()
    {
        _page = new TranscriptionViewModel(
            TestTexts.English,
            _store,
            _worker,
            _runner,
            Substitute.For<IDialogService>(),
            _keeper,
            _viewer,
            _summaries,
            _ffmpeg,
            () => { });
    }

    /// <summary>
    /// Recognize is available only once a file, a model and a device are chosen.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Recognize_NeedsFileModelAndDevice()
    {
        _store.Installed().Returns([SpeechModelCatalog.WhisperLargeV3]);
        _worker.ListDevicesAsync(default).ReturnsForAnyArgs([new ComputeDevice(ComputeDeviceKind.Cpu, -1, "CPU", 1, 2)]);

        _page.RecognizeCommand.CanExecute(null).Should().BeFalse();
        _page.TrySetSource("talk.mp3").Should().BeTrue();
        _page.RecognizeCommand.CanExecute(null).Should().BeFalse();

        _page.RefreshModels();
        await _page.LoadDevicesAsync();

        _page.SelectedModel.Should().Be(SpeechModelCatalog.WhisperLargeV3);
        _page.SelectedDevice.Should().NotBeNull();
        _page.RecognizeCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A discrete video card is chosen before an integrated one and before the processor.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task LoadDevices_PrefersADiscreteVideoCard()
    {
        _worker.ListDevicesAsync(default).ReturnsForAnyArgs(
        [
            new ComputeDevice(ComputeDeviceKind.IntegratedGpu, 0, "iGPU", 1, 2),
            new ComputeDevice(ComputeDeviceKind.Gpu, 1, "GPU", 1, 2),
            new ComputeDevice(ComputeDeviceKind.Cpu, -1, "CPU", 1, 2),
        ]);

        await _page.LoadDevicesAsync();

        _page.SelectedDevice!.Device.Name.Should().Be("GPU");
        _page.Devices.Should().HaveCount(3);
    }

    /// <summary>
    /// When the worker cannot list the devices, the processor is offered alone.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task LoadDevices_FallsBackToTheProcessor()
    {
        _worker.ListDevicesAsync(default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<ComputeDevice>>(new WorkerException("crash", null, 1)));

        await _page.LoadDevicesAsync();

        _page.Devices.Should().ContainSingle().Which.Device.Kind.Should().Be(ComputeDeviceKind.Cpu);
        _page.SelectedDevice!.Title.Should().Be(TestTexts.English.Get("Device.CpuUnnamed"));
    }

    /// <summary>
    /// A file that cannot hold sound is refused with a message.
    /// </summary>
    [Fact]
    public void TrySetSource_RefusesOtherFiles()
    {
        _page.TrySetSource("notes.txt").Should().BeFalse();

        _page.HasSource.Should().BeFalse();
        _page.ErrorMessage.Should().Be(TestTexts.English.Format("Transcription.Unsupported", "notes.txt"));
    }
    /// <summary>
    /// A recognized text is kept in the history, and the result opens from there.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Recognize_KeepsTheTextInTheHistory()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(new KeptRecord(7, true, null));
        _viewer.OpenAsync(7, Arg.Any<CancellationToken>()).Returns(true);

        await _page.RecognizeCommand.ExecuteAsync(null);
        await _page.ShowResultCommand.ExecuteAsync(null);

        _page.Result.Should().Be(transcript);
        _page.HasError.Should().BeFalse();
        await _keeper.Received(1).KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
        await _viewer.Received(1).OpenAsync(7, Arg.Any<CancellationToken>());
        await _viewer.DidNotReceive().ShowAsync(Arg.Any<Transcript>());
    }

    /// <summary>
    /// When the history cannot keep the text, the failure is shown and the text of the session still opens.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Recognize_WhenTheHistoryFails_KeepsTheResult()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<KeptRecord>(new IOException("disk full")));

        await _page.RecognizeCommand.ExecuteAsync(null);
        await _page.ShowResultCommand.ExecuteAsync(null);

        _page.Result.Should().Be(transcript);
        _page.ErrorMessage.Should().Be(TestTexts.English.Format("Transcription.SaveFailed", "disk full"));
        await _viewer.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
        await _viewer.Received(1).ShowAsync(transcript);
    }

    /// <summary>
    /// A sound that could not be kept is reported, and the text kept without it still opens from the history.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Recognize_WhenTheSoundIsNotKept_SaysSo()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(new KeptRecord(8, false, new InvalidDataException("no libopus")));
        _viewer.OpenAsync(8, Arg.Any<CancellationToken>()).Returns(true);

        await _page.RecognizeCommand.ExecuteAsync(null);
        await _page.ShowResultCommand.ExecuteAsync(null);

        _page.ErrorMessage.Should().Be(TestTexts.English.Format("Transcription.AudioNotSaved", "no libopus"));
        await _viewer.Received(1).OpenAsync(8, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Without a server and a model the summary is not tried, and the settings are offered.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_WithoutSettings_OffersTheSettings()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(new KeptRecord(7, true, null));
        await _page.RecognizeCommand.ExecuteAsync(null);

        await _page.SummarizeCommand.ExecuteAsync(null);

        _page.ErrorMessage.Should().Be(TestTexts.English.Get("Summary.NotConfigured"));
        _page.CanFixInSettings.Should().BeTrue();
        await _summaries.DidNotReceiveWithAnyArgs().SummarizeAsync(default, default!, default, default);
    }

    /// <summary>
    /// The summary is written for the kept record, then the record opens with it.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_WritesTheSummaryAndOpensTheRecord()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(new KeptRecord(7, true, null));
        _summaries.IsConfigured.Returns(true);
        _viewer.OpenAsync(7, Arg.Any<CancellationToken>()).Returns(true);
        await _page.RecognizeCommand.ExecuteAsync(null);

        await _page.SummarizeCommand.ExecuteAsync(null);

        await _summaries.Received(1).SummarizeAsync(7, transcript, Arg.Any<IProgress<SummaryProgress>?>(), Arg.Any<CancellationToken>());
        await _viewer.Received(1).OpenAsync(7, Arg.Any<CancellationToken>());
        _page.IsBusy.Should().BeFalse();
        _page.HasError.Should().BeFalse();
    }

    /// <summary>
    /// A server that does not answer is reported on the page.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_ShowsAFailureOfTheServer()
    {
        var transcript = await PrepareRecognitionAsync();
        _keeper.KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(new KeptRecord(7, true, null));
        _summaries.IsConfigured.Returns(true);
        _summaries.SummarizeAsync(default, default!, default, default).ReturnsForAnyArgs(Task.FromException<RecordSummary>(new HttpRequestException("refused")));
        await _page.RecognizeCommand.ExecuteAsync(null);

        await _page.SummarizeCommand.ExecuteAsync(null);

        _page.ErrorMessage.Should().Be(TestTexts.English.Format("Transcription.SummaryFailed", "refused"));
        await _viewer.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
    }

    /// <summary>
    /// Makes everything a recognition needs ready: a file, an installed model, FFmpeg, a device and the runner's result.
    /// </summary>
    /// <returns>The text the runner recognizes.</returns>
    private async Task<Transcript> PrepareRecognitionAsync()
    {
        var model = SpeechModelCatalog.WhisperLargeV3;
        var transcript = new Transcript("talk.mp3", model.Title, "CPU", "en", DateTimeOffset.UnixEpoch, [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello.")]);
        _store.Installed().Returns([model]);
        _store.IsInstalled(model).Returns(true);
        _store.FileOf(model).Returns("model.bin");
        _ffmpeg.Locate().Returns("ffmpeg");
        _worker.ListDevicesAsync(default).ReturnsForAnyArgs([new ComputeDevice(ComputeDeviceKind.Cpu, -1, "CPU", 1, 2)]);
        _runner.RunAsync(default!, default, default).ReturnsForAnyArgs(transcript);

        _page.TrySetSource("talk.mp3");
        _page.RefreshModels();
        await _page.LoadDevicesAsync();

        return transcript;
    }
}
