using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
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
    /// The chosen model of the summaries.
    /// </summary>
    private readonly IChatModelSource _chat = Substitute.For<IChatModelSource>();

    /// <summary>
    /// How many sessions of the model ended.
    /// </summary>
    private int _ended;

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
        _service = new RecordSummaryService(_summarizer, _chat, _history, _time);
    }

    /// <summary>
    /// The summary is made with the chosen model and context, dated and kept with the record; the model is given back after.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_KeepsTheSummary()
    {
        LanguageModelEndpoint.TryCreate("http://localhost:1234/v1", string.Empty, out var endpoint);
        _chat.OpenAsync(default).ReturnsForAnyArgs(_ => new ChatSession(new SummarySettings(endpoint!, "qwen3-8b", 16384), lease: new Release(() => _ended++)));
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
        _ended.Should().Be(1);
    }

    /// <summary>
    /// Without a model the service is not configured and refuses to summarize.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summarize_WithoutAModel_IsRefused()
    {
        _chat.IsAvailable.Returns(false);
        _chat.OpenAsync(default).ThrowsAsyncForAnyArgs(new InvalidOperationException("No model."));

        _service.IsConfigured.Should().BeFalse();
        var act = () => _service.SummarizeAsync(5, _transcript, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _summarizer.DidNotReceiveWithAnyArgs().SummarizeAsync(default!, default!, default, default);
    }

    /// <summary>
    /// Gives a lease back by calling an action.
    /// </summary>
    /// <param name="release">The action.</param>
    private sealed class Release(Action release) : IDisposable
    {
        /// <summary>
        /// Calls the action.
        /// </summary>
        public void Dispose() => release();
    }
}
