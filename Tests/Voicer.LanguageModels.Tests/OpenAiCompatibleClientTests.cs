using System.Net;
using System.Text.Json.Nodes;
using FluentAssertions;
using Voicer.Core.Enums;
using Voicer.Core.Summaries;
using Voicer.Localization;

namespace Voicer.LanguageModels.Tests;

/// <summary>
/// Tests of the client of the OpenAI-compatible API: the list of models, the streamed and the whole answers, the key and
/// the failures worded for the user.
/// </summary>
public sealed class OpenAiCompatibleClientTests
{
    /// <summary>
    /// The server of every test, with a key.
    /// </summary>
    private static readonly LanguageModelEndpoint _endpoint = new(new Uri("http://localhost:1234/v1"), "secret");

    /// <summary>
    /// The conversation of every test.
    /// </summary>
    private static readonly ChatMessage[] _messages = [new(ChatRole.System, "Be brief."), new(ChatRole.User, "Summarize: hello.")];

    /// <summary>
    /// The models are listed sorted, without repeats and empty names, from GET models with the key.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ListModels_ReadsTheList()
    {
        var server = new FakeServer(HttpStatusCode.OK, """{"object":"list","data":[{"id":"qwen3-8b"},{"id":"Gemma-3"},{"id":""},{"id":"qwen3-8b"},{"x":1}]}""", "application/json");

        var models = await Client(server).ListModelsAsync(_endpoint, CancellationToken.None);

        models.Should().Equal("Gemma-3", "qwen3-8b");
        server.Request!.Method.Should().Be(HttpMethod.Get);
        server.Request.RequestUri!.AbsoluteUri.Should().Be("http://localhost:1234/v1/models");
        server.Request.Headers.Authorization!.ToString().Should().Be("Bearer secret");
    }

    /// <summary>
    /// A streamed answer is put together piece by piece, each step reported; roles and reasoning pieces add nothing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Complete_PutsTheStreamTogether()
    {
        var stream = string.Join(
            "\n",
            """data: {"choices":[{"delta":{"role":"assistant"}}]}""",
            string.Empty,
            """data: {"choices":[{"delta":{"reasoning_content":"thinking"}}]}""",
            """data: {"choices":[{"delta":{"content":"Привет"}}]}""",
            ": keep-alive",
            """data: {"choices":[{"delta":{"content":", мир."}}]}""",
            "data: [DONE]",
            """data: {"choices":[{"delta":{"content":"after the end"}}]}""");
        var server = new FakeServer(HttpStatusCode.OK, stream, "text/event-stream");
        var reports = new List<string>();

        var answer = await Client(server).CompleteAsync(_endpoint, "qwen3-8b", _messages, new SyncProgress(reports), CancellationToken.None);

        answer.Should().Be("Привет, мир.");
        reports.Should().Equal("Привет", "Привет, мир.");
        server.Request!.RequestUri!.AbsoluteUri.Should().Be("http://localhost:1234/v1/chat/completions");
        var body = JsonNode.Parse(server.RequestBody!)!;
        body["model"]!.GetValue<string>().Should().Be("qwen3-8b");
        body["stream"]!.GetValue<bool>().Should().BeTrue();
        body["messages"]![0]!["role"]!.GetValue<string>().Should().Be("system");
        body["messages"]![1]!["content"]!.GetValue<string>().Should().Be("Summarize: hello.");
    }

    /// <summary>
    /// A model whose whole answer the server put into the reasoning still gives its answer, streamed or whole.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Complete_FallsBackToTheReasoning()
    {
        var stream = string.Join(
            "\n",
            """data: {"choices":[{"delta":{"reasoning_content":"Сто тысяч "}}]}""",
            """data: {"choices":[{"delta":{"reasoning_content":"рублей [1]."}}]}""",
            "data: [DONE]");
        var streamed = new FakeServer(HttpStatusCode.OK, stream, "text/event-stream");
        var whole = new FakeServer(HttpStatusCode.OK, """{"choices":[{"message":{"content":"","reasoning_content":"Сто тысяч рублей [1]."}}]}""", "application/json");

        (await Client(streamed).CompleteAsync(_endpoint, "m", _messages, null, CancellationToken.None)).Should().Be("Сто тысяч рублей [1].");
        (await Client(whole).CompleteAsync(_endpoint, "m", _messages, null, CancellationToken.None)).Should().Be("Сто тысяч рублей [1].");
    }

    /// <summary>
    /// A server that answers whole instead of streaming is understood too.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Complete_ReadsAWholeAnswer()
    {
        var server = new FakeServer(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":"Done."}}]}""", "application/json");

        var answer = await Client(server).CompleteAsync(_endpoint, "qwen3-8b", _messages, null, CancellationToken.None);

        answer.Should().Be("Done.");
    }

    /// <summary>
    /// A refusal of the server is worded with its code and its own message.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Complete_WordsARefusal()
    {
        var server = new FakeServer(HttpStatusCode.BadRequest, """{"error":{"message":"Model qwen3-8b is not loaded"}}""", "application/json");

        var act = () => Client(server).CompleteAsync(_endpoint, "qwen3-8b", _messages, null, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Summary.ServerError", 400, "Model qwen3-8b is not loaded"));
    }

    /// <summary>
    /// A server that cannot be reached is worded with its address.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ListModels_WordsAnUnreachableServer()
    {
        using var http = new HttpClient(new UnreachableServer());
        var client = new OpenAiCompatibleClient(http);

        var act = () => client.ListModelsAsync(_endpoint, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Summary.Unreachable", _endpoint.BaseAddress));
    }

    /// <summary>
    /// An answer that is not a list of models is worded as unreadable.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ListModels_RefusesAnUnreadableAnswer()
    {
        var server = new FakeServer(HttpStatusCode.OK, "<html>not an API</html>", "text/html");

        var act = () => Client(server).ListModelsAsync(_endpoint, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<InvalidDataException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Get("Summary.BadAnswer"));
    }

    /// <summary>
    /// The vectors come back in the order of the texts, by their index, from POST embeddings.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Embed_ReadsTheVectorsInOrder()
    {
        var server = new FakeServer(HttpStatusCode.OK, """{"data":[{"index":1,"embedding":[0.5,0.25]},{"index":0,"embedding":[1,-1]}]}""", "application/json");

        var vectors = await Client(server).EmbedAsync(_endpoint, "bge-m3", ["первый", "второй"], CancellationToken.None);

        vectors.Should().HaveCount(2);
        vectors[0].Should().Equal(1f, -1f);
        vectors[1].Should().Equal(0.5f, 0.25f);
        server.Request!.RequestUri!.AbsoluteUri.Should().Be("http://localhost:1234/v1/embeddings");
        var body = JsonNode.Parse(server.RequestBody!)!;
        body["input"]![1]!.GetValue<string>().Should().Be("второй");
    }

    /// <summary>
    /// An answer without a vector per text, or with vectors of different lengths, is refused.
    /// </summary>
    /// <param name="body">The answer.</param>
    [Theory]
    [InlineData("""{"data":[{"index":0,"embedding":[1,2]}]}""")]
    [InlineData("""{"data":[{"index":0,"embedding":[1,2]},{"index":1,"embedding":[1]}]}""")]
    [InlineData("""{"data":[{"index":0,"embedding":[1,2]},{"index":0,"embedding":[3,4]}]}""")]
    [InlineData("""{"data":[{"index":0,"embedding":["x",2]},{"index":1,"embedding":[3,4]}]}""")]
    public void Embed_RefusesABrokenAnswer(string body)
    {
        var act = () => OpenAiCompatibleClient.ParseEmbeddings(body, 2);

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A refusal whose <c>error</c> is a plain text, as some servers send when no model is loaded, is worded with that
    /// text instead of ending the application.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Embed_WordsARefusalWithATextError()
    {
        var server = new FakeServer(HttpStatusCode.BadRequest, """{"error":"No models loaded"}""", "application/json");

        var act = () => Client(server).EmbedAsync(_endpoint, "bge-m3", ["первый"], CancellationToken.None);

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Summary.ServerError", 400, "No models loaded"));
    }

    /// <summary>
    /// A refusal whose body is JSON of another shape (a list, a text) is worded with the body as it came.
    /// </summary>
    /// <param name="body">The body of the refusal.</param>
    /// <returns>A task that completes with the test.</returns>
    [Theory]
    [InlineData("""["busy"]""")]
    [InlineData("\"busy\"")]
    public async Task Embed_WordsARefusalOfAnotherShape(string body)
    {
        var server = new FakeServer(HttpStatusCode.ServiceUnavailable, body, "application/json");

        var act = () => Client(server).EmbedAsync(_endpoint, "bge-m3", ["первый"], CancellationToken.None);

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Summary.ServerError", 503, body));
    }

    /// <summary>
    /// A piece of a stream that carries a failure as a plain text is worded as a failure of the server.
    /// </summary>
    [Fact]
    public void ParseDelta_WordsATextError()
    {
        var act = () => OpenAiCompatibleClient.ParseDelta("""{"error":"Model unloaded"}""");

        act.Should().Throw<HttpRequestException>()
            .Which.Should().Match<HttpRequestException>(e => TestTexts.Russian.Describe(e) == TestTexts.Russian.Format("Summary.ServerError", 500, "Model unloaded"));
    }

    /// <summary>
    /// An answer of the right syntax but of another shape (a list for an object, a number for an item) is refused as
    /// unreadable, never as an error of the program.
    /// </summary>
    /// <param name="body">The answer.</param>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("""{"data":[1,2]}""")]
    [InlineData("""{"data":[{"index":0,"embedding":{"x":1}},{"index":1,"embedding":[1]}]}""")]
    public void Embed_RefusesAnAnswerOfAnotherShape(string body)
    {
        var act = () => OpenAiCompatibleClient.ParseEmbeddings(body, 2);

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A list of models or a whole answer of another shape is refused as unreadable, never as an error of the program.
    /// </summary>
    /// <param name="body">The answer.</param>
    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    [InlineData("""{"choices":{"message":"x"}}""")]
    [InlineData("""{"choices":["x"]}""")]
    public void ParseMessage_RefusesAnAnswerOfAnotherShape(string body)
    {
        var act = () => OpenAiCompatibleClient.ParseMessage(body);

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A list of models with items of another shape keeps the readable names and refuses an answer that is no object.
    /// </summary>
    [Fact]
    public void ParseModels_SkipsItemsOfAnotherShape()
    {
        OpenAiCompatibleClient.ParseModels("""{"data":[1,"x",{"id":"qwen3-8b"}]}""").Should().Equal("qwen3-8b");
        FluentActions.Invoking(() => OpenAiCompatibleClient.ParseModels("[1]")).Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A piece of a stream of another shape carries no text instead of ending the application.
    /// </summary>
    /// <param name="data">The data of the piece.</param>
    [Theory]
    [InlineData("[1]")]
    [InlineData("""{"choices":{"delta":"x"}}""")]
    [InlineData("""{"choices":[5]}""")]
    public void ParseDelta_IgnoresAPieceOfAnotherShape(string data)
    {
        OpenAiCompatibleClient.ParseDelta(data).Should().Be((string.Empty, string.Empty));
    }

    /// <summary>
    /// Creates a client over a fake server.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <returns>The client.</returns>
    private static OpenAiCompatibleClient Client(FakeServer server)
    {
        return new OpenAiCompatibleClient(new HttpClient(server));
    }

    /// <summary>
    /// A server whose connection is refused.
    /// </summary>
    private sealed class UnreachableServer : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection refused");
        }
    }

    /// <summary>
    /// Collects the reports at once, on the thread they are made on.
    /// </summary>
    /// <param name="reports">Where the reports go.</param>
    private sealed class SyncProgress(List<string> reports) : IProgress<string>
    {
        /// <inheritdoc />
        public void Report(string value) => reports.Add(value);
    }
}
