using FluentAssertions;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the lines between the application and its worker.
/// </summary>
public sealed class WorkerProtocolTests
{
    /// <summary>
    /// An event survives its way through a line.
    /// </summary>
    [Fact]
    public void Serialize_RoundTrips()
    {
        var devices = new WorkerEvent(WorkerEventKind.Devices)
        {
            Devices = [new ComputeDevice(ComputeDeviceKind.Gpu, 0, "GPU", 1, 2), new ComputeDevice(ComputeDeviceKind.Cpu, -1, "CPU", 3, 4)],
        };
        var segment = new WorkerEvent(WorkerEventKind.Segment) { Segment = new TranscriptSegment(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3), "Привет") };
        var error = new WorkerEvent(WorkerEventKind.Error) { Error = WorkerErrorCode.ModelLoadFailed, Detail = "out of memory" };

        foreach (var original in new[] { devices, segment, error })
        {
            var line = WorkerProtocol.Serialize(original);

            line.Should().NotContain("\n");
            WorkerProtocol.TryParse(line, out var parsed).Should().BeTrue();
            parsed.Should().BeEquivalentTo(original);
        }
    }

    /// <summary>
    /// A line that a native library printed by itself is not an event.
    /// </summary>
    /// <param name="line">The line.</param>
    [Theory]
    [InlineData("ggml_vulkan: Found 2 Vulkan devices")]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_SkipsOtherLines(string? line)
    {
        WorkerProtocol.TryParse(line, out _).Should().BeFalse();
    }
}
