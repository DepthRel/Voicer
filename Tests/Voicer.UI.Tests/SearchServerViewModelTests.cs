using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Voicer.Speech.Llama.Embeddings;
using Voicer.Speech.Llama.Enums;
using Voicer.UI.ViewModels.Formatting;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the llama-server of the search by meaning in the settings: its state in words, what it takes and its stop.
/// </summary>
public sealed class SearchServerViewModelTests
{
    /// <summary>
    /// The name of the model of the tests.
    /// </summary>
    private const string MODEL = "bge-m3";

    /// <summary>
    /// Reads the state of the server and stops it.
    /// </summary>
    private readonly IEmbeddingServerMonitor _monitor = Substitute.For<IEmbeddingServerMonitor>();

    /// <summary>
    /// The clock of the tests.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The view model under test, on a processor of four cores.
    /// </summary>
    private readonly SearchServerViewModel _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchServerViewModelTests"/> class.
    /// </summary>
    public SearchServerViewModelTests()
    {
        _monitor.Status().Returns(EmbeddingServerStatus.Stopped);
        _server = new SearchServerViewModel(TestTexts.Russian, _monitor, _time, cores: 4);
    }

    /// <summary>
    /// No server: it says the next search starts one, and there is nothing to stop.
    /// </summary>
    [Fact]
    public void Stopped_SaysTheSearchStartsIt()
    {
        _server.Refresh();

        _server.StateText.Should().Be(TestTexts.Russian.Get("SearchServer.Stopped"));
        _server.IsRunning.Should().BeFalse();
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

        _server.StateText.Should().Be(TestTexts.Russian.Format("SearchServer.Idle", MODEL, DisplayText.Duration(TimeSpan.FromSeconds(298))));
        _server.ProcessorPercent.Should().BeApproximately(50, 0.001);
        _server.UsageText.Should().Be(TestTexts.Russian.Format("SearchServer.Usage", DisplayText.Bytes(TestTexts.Russian, 700_000_000), "50", 4242));
        _server.StopCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A server busy with a search runs but cannot be stopped.
    /// </summary>
    [Fact]
    public void Busy_CannotBeStopped()
    {
        _monitor.Status().Returns(new EmbeddingServerStatus(EmbeddingServerState.Busy, MODEL, Usage(TimeSpan.Zero), null));

        _server.Refresh();

        _server.StateText.Should().Be(TestTexts.Russian.Format("SearchServer.Busy", MODEL));
        _server.IsRunning.Should().BeTrue();
        _server.StopCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>
    /// The stop ends the server and shows the state that follows.
    /// </summary>
    [Fact]
    public void Stop_EndsTheServer()
    {
        _monitor.Status().Returns(Idle(TimeSpan.Zero, _time.GetUtcNow()), EmbeddingServerStatus.Stopped);
        _server.Refresh();

        _server.StopCommand.Execute(null);

        _monitor.Received(1).TryStop();
        _server.IsRunning.Should().BeFalse();
    }

    /// <summary>
    /// Creates the state of a waiting server.
    /// </summary>
    /// <param name="processorTime">The processor time its process used.</param>
    /// <param name="stopsAt">When it stops by itself.</param>
    /// <returns>The state.</returns>
    private static EmbeddingServerStatus Idle(TimeSpan processorTime, DateTimeOffset stopsAt)
    {
        return new EmbeddingServerStatus(EmbeddingServerState.Idle, MODEL, Usage(processorTime), stopsAt);
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
