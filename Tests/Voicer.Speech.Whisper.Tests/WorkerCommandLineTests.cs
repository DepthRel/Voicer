using FluentAssertions;
using Voicer.Speech.Whisper.Worker;

namespace Voicer.Speech.Whisper.Tests;

/// <summary>
/// Tests of the command line of the worker.
/// </summary>
public sealed class WorkerCommandLineTests
{
    /// <summary>
    /// A recognition names the model, the audio and the device.
    /// </summary>
    [Fact]
    public void TryParse_ReadsARecognition()
    {
        WorkerCommandLine.TryParse(["transcribe", "--model", "m.bin", "--audio", "a.wav", "--device", "1"], out var commandLine, out _).Should().BeTrue();

        commandLine.Should().Be(new WorkerCommandLine("transcribe", "m.bin", "a.wav", 1));
    }

    /// <summary>
    /// The processor is the device when none is named.
    /// </summary>
    [Fact]
    public void TryParse_TakesTheProcessorByDefault()
    {
        WorkerCommandLine.TryParse(["transcribe", "--model", "m.bin", "--audio", "a.wav"], out var commandLine, out _).Should().BeTrue();

        commandLine!.DeviceIndex.Should().Be(-1);
    }

    /// <summary>
    /// Command lines that cannot be run are refused with a reason.
    /// </summary>
    /// <param name="line">The command line, its arguments separated by spaces.</param>
    [Theory]
    [InlineData("")]
    [InlineData("dance")]
    [InlineData("transcribe --model m.bin")]
    [InlineData("transcribe --model m.bin --audio a.wav --device gpu")]
    [InlineData("transcribe --model m.bin --audio a.wav --device -2")]
    [InlineData("transcribe --model")]
    public void TryParse_RefusesInvalidLines(string line)
    {
        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        WorkerCommandLine.TryParse(args, out var commandLine, out var problem).Should().BeFalse();

        commandLine.Should().BeNull();
        problem.Should().NotBeNullOrWhiteSpace();
    }
}
