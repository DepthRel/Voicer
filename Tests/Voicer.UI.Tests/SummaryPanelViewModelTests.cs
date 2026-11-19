using FluentAssertions;
using NSubstitute;
using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.UI.Services.Summaries;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the summary of the window of a record: the kept one, a new one, and a failure that keeps the old one.
/// </summary>
public sealed class SummaryPanelViewModelTests
{
    /// <summary>
    /// The text of the record.
    /// </summary>
    private static readonly Transcript _transcript = new("talk.mp4", "Whisper Small Q5", "CPU", "ru", DateTimeOffset.UnixEpoch, []);

    /// <summary>
    /// A kept summary.
    /// </summary>
    private static readonly RecordSummary _kept = new("- старое", "qwen3-8b", new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.FromHours(3)));

    /// <summary>
    /// Writes and keeps the summaries.
    /// </summary>
    private readonly IRecordSummaryService _summaries = Substitute.For<IRecordSummaryService>();

    /// <summary>
    /// A kept summary is shown with its model and moment, and the button offers to write it again.
    /// </summary>
    [Fact]
    public void Kept_IsShown()
    {
        var panel = Panel(_kept);

        panel.Text.Should().Be("- старое");
        panel.KeptText.Should().Be("- старое");
        panel.Details.Should().Be(TestTexts.Russian.Format("Record.SummaryDetails", "qwen3-8b", _kept.CreatedAt.ToString("g", TestTexts.Russian.Culture)));
        panel.SummarizeLabel.Should().Be(TestTexts.Russian.Get("Record.Resummarize"));
    }

    /// <summary>
    /// Without a summary the panel says so and offers to write one.
    /// </summary>
    [Fact]
    public void Missing_IsOffered()
    {
        var panel = Panel(null);

        panel.HasText.Should().BeFalse();
        panel.Details.Should().Be(TestTexts.Russian.Get("Record.NoSummary"));
        panel.SummarizeLabel.Should().Be(TestTexts.Russian.Get("Record.Summarize"));
    }

    /// <summary>
    /// A new summary is written for the record and shown; it goes into the export.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_ShowsTheNewSummary()
    {
        var fresh = new RecordSummary("- новое", "gemma-3-12b", DateTimeOffset.UnixEpoch);
        _summaries.IsConfigured.Returns(true);
        _summaries.SummarizeAsync(5, _transcript, Arg.Any<IProgress<SummaryProgress>?>(), Arg.Any<CancellationToken>()).Returns(fresh);
        var panel = Panel(_kept);

        await panel.SummarizeCommand.ExecuteAsync(null);

        panel.Text.Should().Be("- новое");
        panel.KeptText.Should().Be("- новое");
        panel.IsWorking.Should().BeFalse();
        panel.HasError.Should().BeFalse();
    }

    /// <summary>
    /// A failed summary keeps the old one and says why.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_ThatFailed_KeepsTheOldOne()
    {
        _summaries.IsConfigured.Returns(true);
        _summaries.SummarizeAsync(default, default!, default, default).ReturnsForAnyArgs(Task.FromException<RecordSummary>(new HttpRequestException("refused")));
        var panel = Panel(_kept);

        await panel.SummarizeCommand.ExecuteAsync(null);

        panel.Text.Should().Be("- старое");
        panel.Error.Should().Be(TestTexts.Russian.Format("Summary.Failed", "refused"));
    }

    /// <summary>
    /// Without a server and a model nothing is asked, and the user is told to choose them.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_WithoutSettings_SaysSo()
    {
        var panel = Panel(null);

        await panel.SummarizeCommand.ExecuteAsync(null);

        panel.Error.Should().Be(TestTexts.Russian.Get("Summary.NotConfigured"));
        await _summaries.DidNotReceiveWithAnyArgs().SummarizeAsync(default, default!, default, default);
    }

    /// <summary>
    /// Creates the panel of record 5.
    /// </summary>
    /// <param name="kept">The kept summary, or <see langword="null"/>.</param>
    /// <returns>The panel.</returns>
    private SummaryPanelViewModel Panel(RecordSummary? kept)
    {
        return new SummaryPanelViewModel(TestTexts.Russian, 5, _transcript, kept, _summaries);
    }
}
