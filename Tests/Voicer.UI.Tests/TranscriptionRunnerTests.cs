using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Models;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;
using Voicer.Media;
using Voicer.UI.Enums;
using Voicer.UI.Services.Transcription;
using Voicer.UI.Services.Worker;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the recognition from start to end with a fake extractor and a fake worker.
/// </summary>
public sealed class TranscriptionRunnerTests : IDisposable
{
    /// <summary>
    /// The temporary folder of the runner.
    /// </summary>
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "voicer-runner-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The extractor of the sound.
    /// </summary>
    private readonly IAudioExtractor _extractor = Substitute.For<IAudioExtractor>();

    /// <summary>
    /// The worker.
    /// </summary>
    private readonly IWorkerClient _worker = Substitute.For<IWorkerClient>();

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The request of every test.
    /// </summary>
    private readonly TranscriptionRequest _request = new(
        "talk.mp4",
        SpeechModelCatalog.WhisperLargeV3,
        "model.bin",
        new ComputeDevice(ComputeDeviceKind.Gpu, 1, "RTX", 0, 0),
        "RTX",
        "vad.bin");

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    /// <summary>
    /// The sound is extracted, recognized on the chosen device, and the temporary file is removed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_ExtractsRecognizesAndCleansUp()
    {
        string? wav = null;
        _extractor.ExtractAsync("talk.mp4", Arg.Do<string>(p =>
            {
                wav = p;
                Directory.CreateDirectory(_temp);
                File.WriteAllBytes(p, [1]);
            }), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromSeconds(5));
        var segment = new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hi");
        _worker.TranscribeAsync("model.bin", Arg.Any<string>(), 1, "vad.bin", Arg.Any<IProgress<WorkerEvent>?>(), Arg.Any<CancellationToken>())
            .Returns(new WorkerTranscription([segment], "en"));
        var stages = new List<TranscriptionStage>();

        var transcript = await Runner().RunAsync(_request, new Services.SyncProgress<TranscriptionProgress>(p => stages.Add(p.Stage)), CancellationToken.None);

        transcript.Segments.Should().Equal(segment);
        transcript.Language.Should().Be("en");
        transcript.ModelName.Should().Be("Whisper Large v3 F16");
        transcript.DeviceName.Should().Be("RTX");
        await _worker.Received(1).TranscribeAsync("model.bin", wav!, 1, "vad.bin", Arg.Any<IProgress<WorkerEvent>?>(), Arg.Any<CancellationToken>());
        stages.Should().StartWith([TranscriptionStage.ExtractingAudio]).And.Contain(TranscriptionStage.LoadingModel);
        File.Exists(wav).Should().BeFalse();
    }

    /// <summary>
    /// A failure of the worker removes the temporary file too.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_CleansUpAfterAFailure()
    {
        string? wav = null;
        _extractor.ExtractAsync(Arg.Any<string>(), Arg.Do<string>(p =>
            {
                wav = p;
                Directory.CreateDirectory(_temp);
                File.WriteAllBytes(p, [1]);
            }), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(TimeSpan.FromSeconds(1));
        _worker.TranscribeAsync(default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(Task.FromException<WorkerTranscription>(new WorkerException("crash", null, 1)));

        var run = () => Runner().RunAsync(_request, null, CancellationToken.None);

        await run.Should().ThrowAsync<WorkerException>();
        File.Exists(wav).Should().BeFalse();
    }

    /// <summary>
    /// The sound files a crashed run left behind are removed.
    /// </summary>
    [Fact]
    public void ClearLeftovers_RemovesOldSoundFiles()
    {
        Directory.CreateDirectory(_temp);
        File.WriteAllBytes(Path.Combine(_temp, "old.wav"), [1]);

        Runner().ClearLeftovers();

        Directory.EnumerateFiles(_temp).Should().BeEmpty();
    }

    /// <summary>
    /// Creates the runner under test.
    /// </summary>
    /// <returns>The runner.</returns>
    private TranscriptionRunner Runner()
    {
        return new TranscriptionRunner(_extractor, _worker, _temp, _time);
    }
}
