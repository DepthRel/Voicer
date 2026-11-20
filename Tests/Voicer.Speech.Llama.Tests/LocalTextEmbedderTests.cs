using FluentAssertions;
using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Embeddings;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the model of the search by meaning run by the own llama-server.
/// </summary>
public sealed class LocalTextEmbedderTests
{
    /// <summary>
    /// The address the server of the tests gives.
    /// </summary>
    private static readonly LanguageModelEndpoint _endpoint = new(new Uri("http://127.0.0.1:5555/v1"), "key");

    /// <summary>
    /// A small model of the tests: a slot of 16 tokens, where a text of up to 8 characters surely fits.
    /// </summary>
    private static readonly EmbeddingModel _model = EmbeddingModelCatalog.BgeM3 with { SlotTokens = 16 };

    /// <summary>
    /// The server of the model.
    /// </summary>
    private readonly ILlamaEmbeddingServer _server = Substitute.For<ILlamaEmbeddingServer>();

    /// <summary>
    /// The client of the server.
    /// </summary>
    private readonly ILanguageModelClient _client = Substitute.For<ILanguageModelClient>();

    /// <summary>
    /// How many times the server was given back.
    /// </summary>
    private int _released;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTextEmbedderTests"/> class.
    /// </summary>
    public LocalTextEmbedderTests()
    {
        _server.AcquireAsync(_model, Arg.Any<CancellationToken>()).Returns(_ => new EmbeddingServerLease(_endpoint, () => _released++));
    }

    /// <summary>
    /// The texts are sent to the server of the model, under the name of the model, cut to fit a slot; the vectors are
    /// kept apart from those of a server, and the server is given back after the request.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Embed_SendsTheTextsToTheOwnServer()
    {
        IReadOnlyList<float[]> vectors = [[1f], [2f]];
        _client.EmbedAsync(default!, default!, default!, default).ReturnsForAnyArgs(vectors);
        var embedder = new LocalTextEmbedder(_server, _client, _model);

        var result = await embedder.EmbedAsync(["short", "a much longer text"], CancellationToken.None);

        result.Should().BeSameAs(vectors);
        embedder.Model.Should().Be("voicer/bge-m3");
        await _client.Received(1).EmbedAsync(
            _endpoint,
            "bge-m3",
            Arg.Is<IReadOnlyList<string>>(texts => texts.SequenceEqual(new[] { "short", "a much l" })),
            Arg.Any<CancellationToken>());
        _released.Should().Be(1);
    }

    /// <summary>
    /// A server that stopped answering at all (its process ended) is reported as such, not as a server of the user to
    /// turn on; the server is given back all the same.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Embed_ReportsAServerThatStopped()
    {
        _client.EmbedAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException<IReadOnlyList<float[]>>(new HttpRequestException("connection reset")));
        var embedder = new LocalTextEmbedder(_server, _client, _model);

        var failure = await ((Func<Task>)(() => embedder.EmbedAsync(["text"], CancellationToken.None))).Should().ThrowAsync<HttpRequestException>();

        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Format("Search.ServerStopped", _model.Name));
        _released.Should().Be(1);
    }

    /// <summary>
    /// A refusal of the server, with its status, is passed on as it is.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Embed_PassesARefusalOn()
    {
        var refusal = new HttpRequestException("too large", null, System.Net.HttpStatusCode.InternalServerError);
        _client.EmbedAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<float[]>>(refusal));
        var embedder = new LocalTextEmbedder(_server, _client, _model);

        var failure = await ((Func<Task>)(() => embedder.EmbedAsync(["text"], CancellationToken.None))).Should().ThrowAsync<HttpRequestException>();

        failure.Which.Should().BeSameAs(refusal);
    }
}
