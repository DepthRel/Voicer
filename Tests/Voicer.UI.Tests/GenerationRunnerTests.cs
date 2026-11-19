using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.Extensions;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Synthesis;
using Voicer.Media;
using Voicer.UI.Enums;
using Voicer.UI.Services.Generation;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of a generation of speech: the pieces, the sample, the joining, the record and the cleaning up.
/// </summary>
public sealed class GenerationRunnerTests : IDisposable
{
    /// <summary>
    /// 16-bit mono at 1000 samples a second: a millisecond is one sample.
    /// </summary>
    private static readonly WaveFormat _format = new(WaveFormat.PCM, 1, 1000, 16);

    /// <summary>
    /// The processor.
    /// </summary>
    private static readonly ComputeDevice _cpu = new(ComputeDeviceKind.Cpu, ComputeDevice.CPU_INDEX, "CPU", 0, 0);

    /// <summary>
    /// A temporary folder of the generations.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-generation-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The synthesizer: a piece lasts a hundred milliseconds.
    /// </summary>
    private readonly ISpeechSynthesizer _synthesizer = Substitute.For<ISpeechSynthesizer>();

    /// <summary>
    /// The preparer of the sample.
    /// </summary>
    private readonly IVoiceSampleExtractor _samples = Substitute.For<IVoiceSampleExtractor>();

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(3)));

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationRunnerTests"/> class.
    /// </summary>
    public GenerationRunnerTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(3), "Test", "Test"));
        _synthesizer.SynthesizeAsync(default!, default!, default!, default).ReturnsForAnyArgs(call =>
        {
            WriteWave(call.ArgAt<string>(2), 100);

            return Task.CompletedTask;
        });
        _samples.ExtractAsync(default!, default!, default).ReturnsForAnyArgs(call =>
        {
            File.WriteAllText(call.ArgAt<string>(1), "sample");

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// Every piece is spoken and joined with its pause; the record is a generation with a fragment per piece, named
    /// after its first six words, and the pieces are gone.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_SpeaksEveryPiece_AndJoinsThem()
    {
        var reports = new List<GenerationProgress>();

        var result = await Runner().RunAsync(Request("Welcome to the app.\nThis is the second line."), new SyncProgress<GenerationProgress>(reports.Add), CancellationToken.None);

        result.Transcript.Kind.Should().Be(RecordKind.Generation);
        result.Transcript.Language.Should().Be("en");
        result.Transcript.ModelName.Should().Be(VoiceModelCatalog.Kokoro.Name);
        result.Transcript.CreatedAt.Should().Be(_time.GetLocalNow());
        result.Transcript.Segments.Select(s => (s.Text, s.Start.TotalMilliseconds, s.End.TotalMilliseconds)).Should().Equal(
            ("Welcome to the app.", 0d, 100d),
            ("This is the second line.", 750d, 850d));
        result.Duration.Should().Be(TimeSpan.FromMilliseconds(850));
        Path.GetFileName(result.AudioPath).Should().Be("Welcome to the app This is.wav");
        File.Exists(result.AudioPath).Should().BeTrue();
        Directory.EnumerateDirectories(Path.GetDirectoryName(result.AudioPath)!).Should().BeEmpty("the pieces are removed");
        reports.Select(r => (r.Stage, r.Part)).Should().Equal(
            (GenerationStage.Speaking, 1),
            (GenerationStage.Speaking, 2),
            (GenerationStage.Joining, 2));
    }

    /// <summary>
    /// The sample the user chose is prepared first, and the model gets the prepared one.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_PreparesTheSample()
    {
        var request = Request("Hello.") with { Voice = new SynthesisVoice(VoiceModelCatalog.Qwen3Tts, "en", null, "/media/me.mp4", _cpu) };

        await Runner().RunAsync(request, null, CancellationToken.None);

        await _samples.Received(1).ExtractAsync("/media/me.mp4", Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _synthesizer.Received(1).SynthesizeAsync(
            Arg.Is<SynthesisVoice>(v => v.SamplePath != null && v.SamplePath != "/media/me.mp4" && v.SamplePath.EndsWith("voice.wav")),
            "Hello.",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A failure of the engine leaves nothing behind.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_RemovesEverything_WhenTheEngineFails()
    {
        _synthesizer.Configure().SynthesizeAsync(default!, default!, default!, default).ThrowsAsyncForAnyArgs(new InvalidDataException("engine"));

        var act = () => Runner().RunAsync(Request("Hello."), null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        Directory.EnumerateFileSystemEntries(_folder).Should().BeEmpty();
    }

    /// <summary>
    /// A text of spaces has nothing to speak: the engine is not started.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_RefusesABlankText()
    {
        var act = () => Runner().RunAsync(Request("  \n "), null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        await _synthesizer.DidNotReceiveWithAnyArgs().SynthesizeAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// A new generation removes the result of the previous one.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_RemovesThePreviousResult()
    {
        var runner = Runner();
        var first = await runner.RunAsync(Request("First."), null, CancellationToken.None);

        var second = await runner.RunAsync(Request("Second."), null, CancellationToken.None);

        File.Exists(first.AudioPath).Should().BeFalse();
        File.Exists(second.AudioPath).Should().BeTrue();
    }

    /// <summary>
    /// Creates the runner of the test.
    /// </summary>
    /// <returns>The runner.</returns>
    private GenerationRunner Runner()
    {
        return new GenerationRunner(_synthesizer, _samples, _folder, _time);
    }

    /// <summary>
    /// Creates a request to speak a text with Kokoro on the processor.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The request.</returns>
    private static GenerationRequest Request(string text)
    {
        return new GenerationRequest(text, new SynthesisVoice(VoiceModelCatalog.Kokoro, "en", "af_heart", null, _cpu), "CPU");
    }

    /// <summary>
    /// Writes a WAV file of silence.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="milliseconds">How long it lasts.</param>
    private static void WriteWave(string path, int milliseconds)
    {
        using var file = File.Create(path);
        var bytes = _format.BytesOf(TimeSpan.FromMilliseconds(milliseconds));
        WaveFile.WriteHeader(file, _format, bytes);
        file.Write(new byte[bytes]);
    }

    /// <summary>
    /// A progress that reports on the calling thread, so the test sees every report at once.
    /// </summary>
    /// <typeparam name="T">The type of the progress.</typeparam>
    /// <param name="report">Receives the reports.</param>
    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        /// <inheritdoc />
        public void Report(T value)
        {
            report(value);
        }
    }
}
