using FluentAssertions;
using NSubstitute;
using Voicer.Core.Enums;
using Voicer.Core.Search;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the search of the records and of the answers to questions about them.
/// </summary>
public sealed class RecordSearchTests
{
    /// <summary>
    /// The server of every test.
    /// </summary>
    private static readonly LanguageModelEndpoint _endpoint = new(new Uri("http://localhost:1234/v1"), null);

    /// <summary>
    /// The model of embeddings of every test.
    /// </summary>
    private static readonly EmbeddingSettings _embeddings = new(_endpoint, "multilingual-e5-large");

    /// <summary>
    /// The passages and their vectors.
    /// </summary>
    private readonly IPassageIndex _index = Substitute.For<IPassageIndex>();

    /// <summary>
    /// The client of the server.
    /// </summary>
    private readonly ILanguageModelClient _client = Substitute.For<ILanguageModelClient>();

    /// <summary>
    /// The search under test.
    /// </summary>
    private readonly RecordSearch _search;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSearchTests"/> class.
    /// </summary>
    public RecordSearchTests()
    {
        _index.SearchWordsAsync(default!, default, default).ReturnsForAnyArgs([Hit(1), Hit(2)]);
        _index.SearchVectorAsync(default!, default!, default, default).ReturnsForAnyArgs([Hit(3), Hit(2)]);
        _client.EmbedAsync(default!, default!, default!, default).ReturnsForAnyArgs(call =>
            (IReadOnlyList<float[]>)[.. call.ArgAt<IReadOnlyList<string>>(2).Select(_ => new float[] { 3, 4 })]);
        _search = new RecordSearch(_index, _client);
    }

    /// <summary>
    /// Without a model of embeddings only the words are searched, and nothing is sent to a server.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task WithoutEmbeddings_SearchesByWords()
    {
        var hits = await _search.SearchAsync("договоров", null, 1, null, CancellationToken.None);

        hits.Should().Equal(Hit(1));
        await _index.Received(1).SearchWordsAsync("\"договор\"*", Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _client.DidNotReceiveWithAnyArgs().EmbedAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// The passages without a vector are embedded first, as passages, a batch at a time; then the query, as a query;
    /// both lists are merged.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task WithEmbeddings_IndexesThenMergesBothSearches()
    {
        _index.CountWithoutVectorAsync("multilingual-e5-large", Arg.Any<CancellationToken>()).Returns(2);
        _index.WithoutVectorAsync("multilingual-e5-large", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PassageText(10, "первый"), new PassageText(11, "второй")], []);
        var reports = new List<SearchProgress>();

        var hits = await _search.SearchAsync("бюджет", _embeddings, 10, new SyncProgress<SearchProgress>(reports), CancellationToken.None);

        hits.Select(h => h.PassageId).Should().Equal(2, 1, 3);
        await _client.Received(1).EmbedAsync(_endpoint, "multilingual-e5-large", Arg.Is<IReadOnlyList<string>>(t => t.SequenceEqual(new[] { "passage: первый", "passage: второй" })), Arg.Any<CancellationToken>());
        await _client.Received(1).EmbedAsync(_endpoint, "multilingual-e5-large", Arg.Is<IReadOnlyList<string>>(t => t.SequenceEqual(new[] { "query: бюджет" })), Arg.Any<CancellationToken>());
        await _index.Received(1).SaveVectorsAsync("multilingual-e5-large", Arg.Is<IReadOnlyList<PassageVector>>(v => v.Count == 2 && v[0].PassageId == 10 && v[0].Vector[0] == 0.6f), Arg.Any<CancellationToken>());
        await _index.Received(1).SearchVectorAsync("multilingual-e5-large", Arg.Is<float[]>(v => v[0] == 0.6f && v[1] == 0.8f), Arg.Any<int>(), Arg.Any<CancellationToken>());
        reports.Should().Equal(new SearchProgress(0, 2), new SearchProgress(2, 2));
    }

    /// <summary>
    /// A question is answered from the passages found, numbered with their file, date and time; the answer comes
    /// without the reasoning, with the passages as its sources.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Question_IsAnsweredFromThePassages()
    {
        IReadOnlyList<ChatMessage>? asked = null;
        _client.CompleteAsync(default!, default!, default!, default, default).ReturnsForAnyArgs(call =>
        {
            asked = call.ArgAt<IReadOnlyList<ChatMessage>>(2);

            return "<think>hm</think>Бюджет утвердили [1].";
        });
        var questions = new RecordQuestions(_search, _client, TestTexts.Russian);
        var chat = new SummarySettings(_endpoint, "qwen3-8b", 8192);

        var answer = await questions.AskAsync("Что с бюджетом?", chat, null, null, null, CancellationToken.None);

        answer.Answer.Should().Be("Бюджет утвердили [1].");
        answer.Sources.Should().Equal(Hit(1), Hit(2));
        asked![0].Should().Be(new ChatMessage(ChatRole.System, TestTexts.Russian.Get("Ask.Prompt.System")));
        var date = DateTimeOffset.UnixEpoch.ToString("g", TestTexts.Russian.Culture);
        var sources = TestTexts.Russian.Format("Ask.Source", 1, "talk.mp3", date, "0:00", "0:05", "text 1") + "\n\n" +
            TestTexts.Russian.Format("Ask.Source", 2, "talk.mp3", date, "0:00", "0:05", "text 2");
        asked[1].Content.Should().Be(TestTexts.Russian.Format("Ask.Prompt.Question", "Что с бюджетом?", sources));
    }

    /// <summary>
    /// When nothing is found, the model is not asked at all.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Question_WithoutPassages_IsNotAsked()
    {
        _index.SearchWordsAsync(default!, default, default).ReturnsForAnyArgs([]);
        var questions = new RecordQuestions(_search, _client, TestTexts.Russian);

        var answer = await questions.AskAsync("Что с бюджетом?", new SummarySettings(_endpoint, "qwen3-8b", 8192), null, null, null, CancellationToken.None);

        answer.Should().Be(new QuestionAnswer(TestTexts.Russian.Get("Ask.NothingFound"), []));
        await _client.DidNotReceiveWithAnyArgs().CompleteAsync(default!, default!, default!, default, default);
    }

    /// <summary>
    /// Creates a passage of a test.
    /// </summary>
    /// <param name="id">The identifier of the passage.</param>
    /// <returns>The passage.</returns>
    private static PassageHit Hit(long id)
    {
        return new PassageHit(id, 1, "talk.mp3", DateTimeOffset.UnixEpoch, TimeSpan.Zero, TimeSpan.FromSeconds(5), "text " + id);
    }

    /// <summary>
    /// Collects the reports at once, on the thread they are made on.
    /// </summary>
    /// <typeparam name="T">The type of a report.</typeparam>
    /// <param name="reports">Where the reports go.</param>
    private sealed class SyncProgress<T>(List<T> reports) : IProgress<T>
    {
        /// <inheritdoc />
        public void Report(T value) => reports.Add(value);
    }
}
