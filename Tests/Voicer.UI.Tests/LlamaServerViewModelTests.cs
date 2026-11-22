using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Servers;
using Voicer.UI.ViewModels.Formatting;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of a llama-server of the application as the user sees it: its state in words, what it takes and its stop.
/// </summary>
public sealed class LlamaServerViewModelTests
{
    /// <summary>
    /// The name of the model of the tests.
    /// </summary>
    private const string MODEL = "bge-m3";

    /// <summary>
    /// Reads the state of the server and stops it.
    /// </summary>
    private readonly ILlamaServerMonitor _monitor = Substitute.For<ILlamaServerMonitor>();

    /// <summary>
    /// The clock of the tests.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The view model under test, on a processor of four cores.
    /// </summary>
    private readonly LlamaServerViewModel _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaServerViewModelTests"/> class.
    /// </summary>
    public LlamaServerViewModelTests()
    {
        _monitor.Status().Returns(LlamaServerStatus.Stopped);
        _server = new LlamaServerViewModel(TestTexts.Russian, _monitor, _time, cores: 4);
    }

    /// <summary>
    /// No server: it says the model starts when needed, it is not shown in the tab strip, and there is nothing to stop.
    /// </summary>
    [Fact]
    public void Stopped_SaysItStartsWhenNeeded()
    {
        _server.Refresh();

        _server.StateText.Should().Be(TestTexts.Russian.Get("LlamaServer.Stopped"));
        _server.IsRunning.Should().BeFalse();
        _server.IsActive.Should().BeFalse();
        _server.HasUsage.Should().BeFalse();
        _server.StopCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// A waiting server shows when it stops by itself, its memory and its share of all the cores since the last
    /// reading, and can be stopped.
    /// </summary>
    [Fact]
    public void Waiting_ShowsWhatItTakes()
    {
        var stopsAt = _time.GetUtcNow() + TimeSpan.FromMinutes(5);
        _monitor.Status().Returns(Idle(TimeSpan.FromSeconds(10), stopsAt));
        _server.Refresh();

        _time.Advance(TimeSpan.FromSeconds(2));
        _monitor.Status().Returns(Idle(TimeSpan.FromSeconds(14), stopsAt));
        _server.Refresh();

        _server.StateText.Should().Be(TestTexts.Russian.Format("LlamaServer.Idle", MODEL, DisplayText.Duration(TimeSpan.FromSeconds(298))));
        _server.ProcessorPercent.Should().BeApproximately(50, 0.001);
        _server.UsageText.Should().Be(TestTexts.Russian.Format("LlamaServer.Usage", DisplayText.Bytes(TestTexts.Russian, 700_000_000), "50", 4242));
        _server.StopCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A server busy with a request can still be stopped at once, and the tab strip shows its model and memory.
    /// </summary>
    [Fact]
    public void Busy_CanBeStoppedAtOnce()
    {
        _monitor.Status().Returns(new LlamaServerStatus(LlamaServerState.Busy, MODEL, Usage(TimeSpan.Zero), null));

        _server.Refresh();

        _server.StateText.Should().Be(TestTexts.Russian.Format("LlamaServer.Busy", MODEL));
        _server.IsRunning.Should().BeTrue();
        _server.StopCommand.CanExecute(null).Should().BeTrue();
        _server.ChipText.Should().Be(TestTexts.Russian.Format("LlamaServer.Chip", MODEL, DisplayText.Bytes(TestTexts.Russian, 700_000_000)));
    }

    /// <summary>
    /// The stop ends the server at once and shows the state that follows.
    /// </summary>
    [Fact]
    public void Stop_EndsTheServer()
    {
        _monitor.Status().Returns(Idle(TimeSpan.Zero, _time.GetUtcNow()), LlamaServerStatus.Stopped);
        _server.Refresh();

        _server.StopCommand.Execute(null);

        _monitor.Received(1).Stop();
        _server.IsRunning.Should().BeFalse();
    }

    /// <summary>
    /// A model that loads is shown by its name, and can be stopped too.
    /// </summary>
    [Fact]
    public void Starting_IsShownAndCanBeStopped()
    {
        _monitor.Status().Returns(new LlamaServerStatus(LlamaServerState.Starting, MODEL, null, null));

        _server.Refresh();

        _server.IsActive.Should().BeTrue();
        _server.ChipText.Should().Be(MODEL);
        _server.StopCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// Creates the state of a waiting server.
    /// </summary>
    /// <param name="processorTime">The processor time its process used.</param>
    /// <param name="stopsAt">When it stops by itself.</param>
    /// <returns>The state.</returns>
    private static LlamaServerStatus Idle(TimeSpan processorTime, DateTimeOffset stopsAt)
    {
        return new LlamaServerStatus(LlamaServerState.Idle, MODEL, Usage(processorTime), stopsAt);
    }

    /// <summary>
    /// Creates what the process of the tests takes.
    /// </summary>
    /// <param name="processorTime">The processor time it used.</param>
    /// <returns>The usage.</returns>
    private static ServerUsage Usage(TimeSpan processorTime)
    {
        return new ServerUsage(4242, 700_000_000, processorTime);
    }
}
