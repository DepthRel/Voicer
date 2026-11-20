using FluentAssertions;
using NSubstitute;
using Voicer.Core.Summaries;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the summaries in the settings: the remembered choice, the programs, the check of the server and the models.
/// </summary>
public sealed class SummarySettingsViewModelTests
{
    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly ISummaryPreferencesStore _store = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// The client of the server.
    /// </summary>
    private readonly ILanguageModelClient _client = Substitute.For<ILanguageModelClient>();

    /// <summary>
    /// The choice of the source of the search by meaning; by words only unless a test says otherwise.
    /// </summary>
    private readonly ISearchSourceStore _search = Substitute.For<ISearchSourceStore>();

    /// <summary>
    /// The remembered choice is shown: the program of its address, the model, the context.
    /// </summary>
    [Fact]
    public void Load_ShowsTheRememberedChoice()
    {
        _store.Load().Returns(new SummaryPreferences("http://localhost:11434/v1", "key", "llama3.1:8b", 16384, "bge-m3"));

        var settings = Settings();

        settings.SelectedPreset!.Name.Should().Be("Ollama");
        settings.SelectedModel.Should().Be("llama3.1:8b");
        settings.SelectedEmbeddingModel.Should().Be("bge-m3");
        settings.Models.Should().Equal("llama3.1:8b", "bge-m3");
        settings.ContextTokens.Should().Be(16384);
        _store.DidNotReceiveWithAnyArgs().Save(default!);
    }

    /// <summary>
    /// A check offers a multilingual model of embeddings and never a model of embeddings for the answers.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_PicksTheModelsByTheirKind()
    {
        _store.Load().Returns(SummaryPreferences.Default);
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(["bge-m3", "nomic-embed-text-v1.5", "qwen3-8b"]);
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.SelectedModel.Should().Be("qwen3-8b");
        settings.SelectedEmbeddingModel.Should().Be("bge-m3");
        _store.Received().Save(Arg.Is<SummaryPreferences>(p => p.Model == "qwen3-8b" && p.EmbeddingModel == "bge-m3"));
    }

    /// <summary>
    /// Choosing a program fills its address, and the choice is remembered.
    /// </summary>
    [Fact]
    public void Preset_FillsTheAddress()
    {
        _store.Load().Returns(SummaryPreferences.Default);
        var settings = Settings();

        settings.SelectedPreset = LanguageModelPreset.All.Single(p => p.Name == "Ollama");

        settings.Address.Should().Be("http://localhost:11434/v1");
        _store.Received().Save(Arg.Is<SummaryPreferences>(p => p.Address == "http://localhost:11434/v1"));
    }

    /// <summary>
    /// The check lists the models of the server, keeps the chosen one when the server has it, and remembers it.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_ListsTheModels()
    {
        _store.Load().Returns(SummaryPreferences.Default with { Model = "qwen3-8b" });
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(["gemma-3-12b", "qwen3-8b"]);
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Models.Should().Equal("gemma-3-12b", "qwen3-8b");
        settings.SelectedModel.Should().Be("qwen3-8b");
        settings.Status.Should().Be(TestTexts.Russian.Format("Summary.Connected", 2));
        settings.IsStatusError.Should().BeFalse();
        await _client.Received(1).ListModelsAsync(Arg.Is<LanguageModelEndpoint>(e => e.BaseAddress.AbsoluteUri == "http://localhost:1234/v1"), Arg.Any<CancellationToken>());
        await _client.DidNotReceiveWithAnyArgs().EmbedAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// The check asks the chosen model of embeddings for a vector, when the search by meaning uses the server, and a
    /// model that makes it is reported as connected.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_TriesTheModelOfEmbeddings()
    {
        _search.Load().Returns(SearchSource.Server);
        _store.Load().Returns(SummaryPreferences.Default with { EmbeddingModel = "text-embedding-nomic-embed-text-v1.5" });
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(["nvidia/nemotron-3-nano-4b", "text-embedding-nomic-embed-text-v1.5"]);
        _client.EmbedAsync(default!, default!, default!, default).ReturnsForAnyArgs([[0.1f, 0.2f]]);
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Status.Should().Be(TestTexts.Russian.Format("Summary.Connected", 2));
        settings.IsStatusError.Should().BeFalse();
        await _client.Received(1).EmbedAsync(Arg.Any<LanguageModelEndpoint>(), "text-embedding-nomic-embed-text-v1.5", Arg.Is<IReadOnlyList<string>>(t => t.Count == 1), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the search by meaning does not use the server, its model of embeddings is not tried: a refusal would only
    /// confuse.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_LeavesTheModelOfEmbeddingsAloneWhenTheSearchDoesNotUseIt()
    {
        _search.Load().Returns(SearchSource.Local);
        _store.Load().Returns(SummaryPreferences.Default with { EmbeddingModel = "text-embedding-nomic-embed-text-v1.5" });
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(["qwen3-8b", "text-embedding-nomic-embed-text-v1.5"]);
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Status.Should().Be(TestTexts.Russian.Format("Summary.Connected", 2));
        await _client.DidNotReceiveWithAnyArgs().EmbedAsync(default!, default!, default!, default);
    }

    /// <summary>
    /// A chat model chosen for embeddings is refused by the server (LM Studio says no model is loaded); the check
    /// names it and keeps the choice, so the search of the history does not fail with no explanation.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_ReportsAModelThatMakesNoEmbeddings()
    {
        _search.Load().Returns(SearchSource.Server);
        _store.Load().Returns(SummaryPreferences.Default with { Model = "nvidia/nemotron-3-nano-4b", EmbeddingModel = "nvidia/nemotron-3-nano-4b" });
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(["nvidia/nemotron-3-nano-4b", "text-embedding-nomic-embed-text-v1.5"]);
        _client.EmbedAsync(default!, default!, default!, default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<float[]>>(new HttpRequestException("No models loaded.")));
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Status.Should().Be(TestTexts.Russian.Format("Summary.EmbeddingRefused", 2, "nvidia/nemotron-3-nano-4b", "No models loaded."));
        settings.IsStatusError.Should().BeTrue();
        settings.SelectedEmbeddingModel.Should().Be("nvidia/nemotron-3-nano-4b");
        settings.IsChecking.Should().BeFalse();
    }

    /// <summary>
    /// An address that is not an API address is refused before the server is asked.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_RefusesAnInvalidAddress()
    {
        _store.Load().Returns(SummaryPreferences.Default with { Address = "localhost:1234" });
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Status.Should().Be(TestTexts.Russian.Get("Summary.InvalidAddress"));
        settings.IsStatusError.Should().BeTrue();
        await _client.DidNotReceiveWithAnyArgs().ListModelsAsync(default!, default);
    }

    /// <summary>
    /// A server that does not answer is reported.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Check_ReportsAFailure()
    {
        _store.Load().Returns(SummaryPreferences.Default);
        _client.ListModelsAsync(default!, default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<string>>(new HttpRequestException("refused")));
        var settings = Settings();

        await settings.CheckCommand.ExecuteAsync(null);

        settings.Status.Should().Be("refused");
        settings.IsStatusError.Should().BeTrue();
    }

    /// <summary>
    /// Creates the view model under test, in Russian.
    /// </summary>
    /// <returns>The view model.</returns>
    private SummarySettingsViewModel Settings()
    {
        return new SummarySettingsViewModel(TestTexts.Russian, _store, _client, _search);
    }
}
