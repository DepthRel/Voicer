using FluentAssertions;
using NSubstitute;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Servers;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Summaries;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the choice of the model that writes the summaries and the answers: a model of the server, a model of Voicer
/// in its own llama-server, or none.
/// </summary>
public sealed class ChatModelSourceTests
{
    /// <summary>
    /// The address of the llama-server of the tests.
    /// </summary>
    private const string LOCAL_ADDRESS = "http://127.0.0.1:50123/v1";

    /// <summary>
    /// The chosen source.
    /// </summary>
    private readonly ISummarySourceStore _source = Substitute.For<ISummarySourceStore>();

    /// <summary>
    /// The models of Voicer.
    /// </summary>
    private readonly IChatModelStore _models = Substitute.For<IChatModelStore>();

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp = Substitute.For<ILlamaCppLocator>();

    /// <summary>
    /// The llama-server of the models of Voicer.
    /// </summary>
    private readonly ILlamaModelServer<ChatModel> _server = Substitute.For<ILlamaModelServer<ChatModel>>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// How many leases were given back.
    /// </summary>
    private int _released;

    /// <summary>
    /// The source under test.
    /// </summary>
    private readonly ChatModelSource _chat;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatModelSourceTests"/> class.
    /// </summary>
    public ChatModelSourceTests()
    {
        _preferences.Load().Returns(new SummaryPreferences("http://localhost:1234/v1", string.Empty, "qwen3-8b", 16384, string.Empty));
        _llamaCpp.LocateServer().Returns("llama-server");
        LanguageModelEndpoint.TryCreate(LOCAL_ADDRESS, "key", out var endpoint);
        _server.AcquireAsync(default!, default).ReturnsForAnyArgs(_ => new LlamaServerLease(endpoint!, () => _released++));
        _chat = new ChatModelSource(_source, _models, _llamaCpp, _server, _preferences);
    }

    /// <summary>
    /// A model of the server is taken from the settings of the server; no llama-server is started.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Server_UsesTheModelOfTheServer()
    {
        _source.Load().Returns(ModelSource.Server);

        _chat.IsAvailable.Should().BeTrue();
        using var session = await _chat.OpenAsync(CancellationToken.None);

        session.Settings.Model.Should().Be("qwen3-8b");
        session.Settings.ContextTokens.Should().Be(16384);
        await _server.DidNotReceiveWithAnyArgs().AcquireAsync(default!, default);
    }

    /// <summary>
    /// A model of Voicer runs in its llama-server for as long as the session lasts, with the context it was loaded with.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Local_HoldsItsServerForTheSession()
    {
        _source.Load().Returns(ModelSource.Local);
        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);

        _chat.IsAvailable.Should().BeTrue();
        var session = await _chat.OpenAsync(CancellationToken.None);

        session.Settings.Model.Should().Be(ChatModelCatalog.Qwen3Small.Id);
        session.Settings.ContextTokens.Should().Be(ChatModelCatalog.Qwen3Small.ContextTokens);
        session.Settings.Endpoint.BaseAddress.AbsoluteUri.Should().Be(LOCAL_ADDRESS);
        await _server.Received(1).AcquireAsync(ChatModelCatalog.Qwen3Small, CancellationToken.None);
        _released.Should().Be(0);

        session.Dispose();
        _released.Should().Be(1);
    }

    /// <summary>
    /// Nothing is available when the summaries are off, or when the model of Voicer or llama.cpp is missing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Missing_IsNotAvailable()
    {
        _source.Load().Returns(ModelSource.Off);
        _chat.IsAvailable.Should().BeFalse();
        await _chat.Invoking(c => c.OpenAsync(CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();

        _source.Load().Returns(ModelSource.Local);
        _chat.IsAvailable.Should().BeFalse("no model of Voicer is downloaded");

        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        _llamaCpp.LocateServer().Returns((string?)null);
        _chat.IsAvailable.Should().BeFalse("llama.cpp is not installed");
    }

    /// <summary>
    /// A model of Voicer stopped in the middle of the work is reported as stopped, not as a server that does not answer.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Local_StoppedMidway_IsReportedAsStopped()
    {
        _source.Load().Returns(ModelSource.Local);
        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        using var session = await _chat.OpenAsync(CancellationToken.None);

        var act = () => session.RunAsync<string>(_ => throw new HttpRequestException("connection refused"));

        var failure = await act.Should().ThrowAsync<HttpRequestException>();
        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Format("Summary.LocalStopped", ChatModelCatalog.Qwen3Small.Name));
    }

    /// <summary>
    /// A model of Voicer unloaded while its answer streams in (the connection ends in the middle of the answer) is
    /// reported as unloaded too, in the words of the language pack.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Local_UnloadedWhileStreaming_IsReportedAsStopped()
    {
        _source.Load().Returns(ModelSource.Local);
        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        using var session = await _chat.OpenAsync(CancellationToken.None);

        var act = () => session.RunAsync<string>(_ => throw new HttpIOException(HttpRequestError.ResponseEnded));

        var failure = await act.Should().ThrowAsync<IOException>();
        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Format("Summary.LocalStopped", ChatModelCatalog.Qwen3Small.Name));
    }
}
