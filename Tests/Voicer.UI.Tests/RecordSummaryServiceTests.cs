using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Summaries;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the summary of a record: the settings it is made with, its date, and its keeping in the history.
/// </summary>
public sealed class RecordSummaryServiceTests
{
    /// <summary>
    /// The text of the record.
    /// </summary>
    private static readonly Transcript _transcript = new("talk.mp4", "Whisper Small Q5", "CPU", "ru", DateTimeOffset.UnixEpoch, []);

    /// <summary>
    /// Writes the summaries.
    /// </summary>
    private readonly ISummarizer _summarizer = Substitute.For<ISummarizer>();

    /// <summary>
    /// Remembers the choice of the user.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// The history.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// The clock.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly RecordSummaryService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSummaryServiceTests"/> class.
    /// </summary>
    public RecordSummaryServiceTests()
    {
        _service = new RecordSummaryService(_summarizer, _preferences, _history, _time);
    }

    /// <summary>
    /// The summary is made with the chosen server, model and context, dated and kept with the record.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_KeepsTheSummary()
    {
        _preferences.Load().Returns(new SummaryPreferences("http://localhost:1234/v1", string.Empty, "qwen3-8b", 16384, string.Empty));
        _summarizer.SummarizeAsync(default!, default!, default, default).ReturnsForAnyArgs("- point");
        _history.SaveSummaryAsync(default, default!, default).ReturnsForAnyArgs(true);

        var summary = await _service.SummarizeAsync(5, _transcript, null, CancellationToken.None);

        summary.Should().Be(new RecordSummary("- point", "qwen3-8b", _time.GetLocalNow()));
        await _summarizer.Received(1).SummarizeAsync(
            _transcript,
            Arg.Is<SummarySettings>(s => s.Model == "qwen3-8b" && s.ContextTokens == 16384 && s.Endpoint.BaseAddress.AbsoluteUri == "http://localhost:1234/v1"),
            null,
            CancellationToken.None);
        await _history.Received(1).SaveSummaryAsync(5, summary, CancellationToken.None);
    }

    /// <summary>
    /// Without a model the service is not configured and refuses to summarize.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_WithoutAModel_IsRefused()
    {
        _preferences.Load().Returns(SummaryPreferences.Default);

        _service.IsConfigured.Should().BeFalse();
        var act = () => _service.SummarizeAsync(5, _transcript, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _summarizer.DidNotReceiveWithAnyArgs().SummarizeAsync(default!, default!, default, default);
    }
}
