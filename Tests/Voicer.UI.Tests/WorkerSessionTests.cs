using FluentAssertions;
using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Localization;
using Voicer.UI.Services.Worker;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the following of one run of the worker.
/// </summary>
public sealed class WorkerSessionTests
{
    /// <summary>
    /// Events are handed on; other lines are skipped.
    /// </summary>
    [Fact]
    public void HandleLine_HandsEventsOn()
    {
        var received = new List<WorkerEvent>();
        var session = new WorkerSession(received.Add);

        session.HandleLine("ggml_vulkan: Found 2 Vulkan devices");
        session.HandleLine(WorkerProtocol.Serialize(new WorkerEvent(WorkerEventKind.Progress) { Percent = 10 }));
        session.Complete(0, null);

        received.Should().ContainSingle().Which.Percent.Should().Be(10);
    }

    /// <summary>
    /// A failure the worker named wins over its exit code and is worded in the language of the reader.
    /// </summary>
    [Fact]
    public void Complete_ReportsTheNamedFailure()
    {
        var session = new WorkerSession(_ => { });
        session.HandleLine(WorkerProtocol.Serialize(new WorkerEvent(WorkerEventKind.Error) { Error = WorkerErrorCode.ModelLoadFailed, Detail = "vk" }));

        var complete = () => session.Complete(-1073740791, "crash");

        var failure = complete.Should().Throw<WorkerException>().Which;
        failure.Code.Should().Be(WorkerErrorCode.ModelLoadFailed);
        TestTexts.Russian.Describe(failure).Should().Be(TestTexts.Russian.Format("Worker.Error.ModelLoadFailed", "vk"));
    }

    /// <summary>
    /// A worker that ended without a word is a crash, with its exit code.
    /// </summary>
    [Fact]
    public void Complete_ReportsACrash()
    {
        var session = new WorkerSession(_ => { });

        var complete = () => session.Complete(127, "driver");

        var failure = complete.Should().Throw<WorkerException>().Which;
        failure.Code.Should().BeNull();
        failure.ExitCode.Should().Be(127);
        TestTexts.English.Describe(failure).Should().Be(TestTexts.English.Format("Worker.Crashed", 127, "driver"));
    }
}
