using FluentAssertions;
using NSubstitute;
using Voicer.Core.Enums;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the summarizer: one request for a short text, the parts and their joining for a long one, the instructions
/// in the language of the interface, and no reasoning in the answer.
/// </summary>
public sealed class SummarizerTests
{
    /// <summary>
    /// The server of every test.
    /// </summary>
    private static readonly LanguageModelEndpoint _endpoint = new(new Uri("http://localhost:1234/v1"), null);

    /// <summary>
    /// The client of the server.
    /// </summary>
    private readonly ILanguageModelClient _client = Substitute.For<ILanguageModelClient>();

    /// <summary>
    /// The conversations the client was asked to answer.
    /// </summary>
    private readonly List<IReadOnlyList<ChatMessage>> _asked = [];

    /// <summary>
    /// The summarizer under test, in Russian.
    /// </summary>
    private readonly Summarizer _summarizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummarizerTests"/> class.
    /// </summary>
    public SummarizerTests()
    {
        _client.CompleteAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(call =>
        {
            var messages = call.ArgAt<IReadOnlyList<ChatMessage>>(2);
            _asked.Add(messages);
            var answer = "<think>hm</think>Answer " + _asked.Count;
            call.ArgAt<IProgress<string>?>(3)?.Report(answer);

            return answer;
        });
        _summarizer = new Summarizer(_client, TestTexts.Russian);
    }

    /// <summary>
    /// A text that fits is summarized in one request with the instructions of the language of the interface; the
    /// summary comes without the reasoning, also while it arrives.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ShortText_IsSummarizedAtOnce()
    {
        var reports = new List<SummaryProgress>();

        var summary = await _summarizer.SummarizeAsync(Talk("Привет.", "Как дела?"), Settings(8192), new SyncProgress(reports), CancellationToken.None);

        summary.Should().Be("Answer 1");
        _asked.Should().ContainSingle();
        _asked[0][0].Should().Be(new ChatMessage(ChatRole.System, TestTexts.Russian.Get("Summary.Prompt.System")));
        _asked[0][1].Content.Should().Be(TestTexts.Russian.Format("Summary.Prompt.Whole", "talk.mp3", "Привет." + Environment.NewLine + "Как дела?"));
        reports.Should().ContainEquivalentOf(new SummaryProgress(1, 1, "Answer 1"));
        await _client.Received(1).CompleteAsync(_endpoint, "qwen3-8b", Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A text longer than the context is read part by part, then the notes are joined into the summary.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task LongText_IsSummarizedPartByPart()
    {
        var limit = (int)(Summarizer.PartTokens(SummarySettings.MIN_CONTEXT_TOKENS) * 2.5);
        var line = new string('а', limit - 10);

        var summary = await _summarizer.SummarizeAsync(Talk(line, line, line), Settings(SummarySettings.MIN_CONTEXT_TOKENS), null, CancellationToken.None);

        _asked.Should().HaveCount(4);
        _asked[0][1].Content.Should().Be(TestTexts.Russian.Format("Summary.Prompt.Part", 1, 3, "talk.mp3", line));
        _asked[2][1].Content.Should().Be(TestTexts.Russian.Format("Summary.Prompt.Part", 3, 3, "talk.mp3", line));
        _asked[3][1].Content.Should().Be(TestTexts.Russian.Format("Summary.Prompt.Combine", "talk.mp3", "Answer 1\n\nAnswer 2\n\nAnswer 3"));
        summary.Should().Be("Answer 4");
    }

    /// <summary>
    /// A text without speech is refused before any request.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task EmptyText_IsRefused()
    {
        var act = () => _summarizer.SummarizeAsync(Talk(), Settings(8192), null, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<InvalidDataException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Get("Summary.NoText"));
        _asked.Should().BeEmpty();
    }

    /// <summary>
    /// An answer that is only reasoning counts as no answer.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task EmptyAnswer_IsAFailure()
    {
        _client.CompleteAsync(default!, default!, default!, default, default).ReturnsForAnyArgs("<think>only this</think>");

        var act = () => _summarizer.SummarizeAsync(Talk("Привет."), Settings(8192), null, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<InvalidDataException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Summary.EmptyAnswer", "qwen3-8b"));
    }

    /// <summary>
    /// The part of the text grows with the context of the model and stays positive for the smallest one.
    /// </summary>
    [Fact]
    public void PartTokens_FollowsTheContext()
    {
        Summarizer.PartTokens(SummarySettings.MIN_CONTEXT_TOKENS).Should().BePositive();
        Summarizer.PartTokens(32768).Should().BeGreaterThan(Summarizer.PartTokens(8192));
        Summarizer.PartTokens(10).Should().Be(Summarizer.PartTokens(SummarySettings.MIN_CONTEXT_TOKENS));
    }

    /// <summary>
    /// Creates the settings of a test.
    /// </summary>
    /// <param name="contextTokens">The context of the model.</param>
    /// <returns>The settings.</returns>
    private static SummarySettings Settings(int contextTokens)
    {
        return new SummarySettings(_endpoint, "qwen3-8b", contextTokens);
    }

    /// <summary>
    /// Creates a recognized text with a fragment per text.
    /// </summary>
    /// <param name="texts">The texts of the fragments.</param>
    /// <returns>The transcript.</returns>
    private static Transcript Talk(params string[] texts)
    {
        return new Transcript(
            "talk.mp3",
            "Whisper Small Q5",
            "CPU",
            "ru",
            DateTimeOffset.UnixEpoch,
            [.. texts.Select((t, i) => new TranscriptSegment(TimeSpan.FromSeconds(i), TimeSpan.FromSeconds(i + 1), t))]);
    }

    /// <summary>
    /// Collects the reports at once, on the thread they are made on.
    /// </summary>
    /// <param name="reports">Where the reports go.</param>
    private sealed class SyncProgress(List<SummaryProgress> reports) : IProgress<SummaryProgress>
    {
        /// <inheritdoc />
        public void Report(SummaryProgress value) => reports.Add(value);
    }
}
