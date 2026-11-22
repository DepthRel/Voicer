using FluentAssertions;
using NSubstitute;
using Voicer.Core.Search;
using Voicer.Core.Summaries;
using Voicer.UI.Services.Search;
using Voicer.UI.Services.Summaries;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the questions to the history: they go to the chosen model of the summaries, held while it answers.
/// </summary>
public sealed class RecordSearchServiceTests
{
    /// <summary>
    /// Answers the questions.
    /// </summary>
    private readonly IRecordQuestions _questions = Substitute.For<IRecordQuestions>();

    /// <summary>
    /// The chosen model of the summaries and the answers.
    /// </summary>
    private readonly IChatModelSource _chat = Substitute.For<IChatModelSource>();

    /// <summary>
    /// The service under test.
    /// </summary>
    private readonly RecordSearchService _service;

    /// <summary>
    /// How many sessions of the model ended.
    /// </summary>
    private int _ended;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSearchServiceTests"/> class.
    /// </summary>
    public RecordSearchServiceTests()
    {
        var embedders = Substitute.For<ITextEmbedderSource>();
        _service = new RecordSearchService(Substitute.For<IRecordSearch>(), _questions, _chat, embedders);
    }

    /// <summary>
    /// Questions can be asked only while a model of the summaries can be used.
    /// </summary>
    [Fact]
    public void CanAsk_FollowsTheModelOfTheSummaries()
    {
        _chat.IsAvailable.Returns(true);
        _service.CanAsk.Should().BeTrue();

        _chat.IsAvailable.Returns(false);
        _service.CanAsk.Should().BeFalse();
    }

    /// <summary>
    /// A question is answered by the model of the session, which is given back once the answer is there.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Ask_HoldsTheModelUntilTheAnswer()
    {
        LanguageModelEndpoint.TryCreate("http://127.0.0.1:50123/v1", "key", out var endpoint);
        var settings = new SummarySettings(endpoint!, "qwen3-1.7b", 8192);
        _chat.OpenAsync(default).ReturnsForAnyArgs(_ => new ChatSession(settings, "Qwen3-1.7B", new Release(() => _ended++)));
        var answer = new QuestionAnswer("Yes.", []);
        _questions.AskAsync(default!, default!, default, default, default, default).ReturnsForAnyArgs(answer);

        (await _service.AskAsync("Was it agreed?", null, null, CancellationToken.None)).Should().Be(answer);

        await _questions.Received(1).AskAsync("Was it agreed?", settings, Arg.Any<ITextEmbedder?>(), null, null, CancellationToken.None);
        _ended.Should().Be(1);
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
