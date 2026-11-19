using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;
using Voicer.Speech.Whisper.Devices;
using Voicer.Speech.Whisper.Engine;
using Voicer.Speech.Whisper.Worker;

namespace Voicer.Speech.Whisper.Tests;

/// <summary>
/// Tests of the worker host: what it writes for each command and how it ends.
/// </summary>
public sealed class WorkerHostTests : IDisposable
{
    /// <summary>
    /// A file that plays both the model and the audio.
    /// </summary>
    private readonly string _file = Path.GetTempFileName();

    /// <summary>
    /// The prober of the devices.
    /// </summary>
    private readonly IDeviceProbe _probe = Substitute.For<IDeviceProbe>();

    /// <summary>
    /// The speech engine.
    /// </summary>
    private readonly ISpeechEngine _engine = Substitute.For<ISpeechEngine>();

    /// <summary>
    /// The output of the worker.
    /// </summary>
    private readonly StringWriter _output = new();

    /// <summary>
    /// Removes the temporary file.
    /// </summary>
    public void Dispose()
    {
        File.Delete(_file);
        _output.Dispose();
    }

    /// <summary>
    /// The devices command writes the devices.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Devices_WritesTheDevices()
    {
        var device = new ComputeDevice(ComputeDeviceKind.Gpu, 0, "GPU", 1, 2);
        _probe.Probe().Returns([device]);

        var code = await Host().RunAsync(["devices"], CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_OK);
        Events().Single().Devices.Should().Equal(device);
    }

    /// <summary>
    /// A recognition writes the loading, the progress, the pieces and the end, in that order.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Transcribe_WritesTheRecognition()
    {
        var segment = new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello");
        _engine.TranscribeAsync(_file, _file, 1, null, Arg.Any<ITranscriptionListener>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var listener = call.Arg<ITranscriptionListener>();
                listener.ModelLoaded();
                listener.Progress(50);
                listener.Segment(segment);

                return Task.FromResult<string?>("en");
            });

        var code = await Host().RunAsync(Transcribe(), CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_OK);
        Events().Select(e => e.Kind).Should().Equal(
            WorkerEventKind.ModelLoaded, WorkerEventKind.Progress, WorkerEventKind.Segment, WorkerEventKind.Done);
        Events()[2].Segment.Should().Be(segment);
        Events()[3].Language.Should().Be("en");
    }

    /// <summary>
    /// A model that cannot be loaded is named as such.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Transcribe_ReportsAModelThatDoesNotLoad()
    {
        _engine.TranscribeAsync(default!, default!, default, default, default!, default).ReturnsForAnyArgs(Task.FromException<string?>(new ModelLoadException("no memory")));

        var code = await Host().RunAsync(Transcribe(), CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_FAILED);
        Events().Single().Error.Should().Be(WorkerErrorCode.ModelLoadFailed);
    }

    /// <summary>
    /// A missing model is refused before the engine starts.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Transcribe_RefusesAMissingModel()
    {
        var code = await Host().RunAsync(["transcribe", "--model", _file + ".missing", "--audio", _file], CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_FAILED);
        Events().Single().Error.Should().Be(WorkerErrorCode.ModelNotFound);
        await _engine.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default!, default, default, default!, default);
    }

    /// <summary>
    /// A cancelled recognition ends quietly with its own code.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Transcribe_EndsQuietlyWhenCancelled()
    {
        _engine.TranscribeAsync(default!, default!, default, default, default!, default).ThrowsAsyncForAnyArgs(new OperationCanceledException());

        var code = await Host().RunAsync(Transcribe(), CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_CANCELLED);
        Events().Should().BeEmpty();
    }

    /// <summary>
    /// An invalid command line is refused with its own code.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Run_RefusesAnInvalidCommandLine()
    {
        var code = await Host().RunAsync(["dance"], CancellationToken.None);

        code.Should().Be(WorkerHost.EXIT_INVALID_ARGUMENTS);
        Events().Single().Error.Should().Be(WorkerErrorCode.InvalidArguments);
    }

    /// <summary>
    /// Creates the host under test.
    /// </summary>
    /// <returns>The host.</returns>
    private WorkerHost Host()
    {
        return new WorkerHost(_probe, _engine, _output);
    }

    /// <summary>
    /// Gives the command line of a recognition of the temporary file on the second video card.
    /// </summary>
    /// <returns>The command line.</returns>
    private string[] Transcribe()
    {
        return ["transcribe", "--model", _file, "--audio", _file, "--device", "1"];
    }

    /// <summary>
    /// Reads the events the host wrote.
    /// </summary>
    /// <returns>The events, in order.</returns>
    private List<WorkerEvent> Events()
    {
        return [.. _output.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => WorkerProtocol.TryParse(line, out var e) ? e! : throw new InvalidDataException(line))];
    }
}
