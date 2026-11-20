using FluentAssertions;
using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Embeddings;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the search by meaning in the settings: the one choice of its source and why a choice does not work yet.
/// </summary>
public sealed class SearchSettingsViewModelTests
{
    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly ISearchSourceStore _store = Substitute.For<ISearchSourceStore>();

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models = Substitute.For<IEmbeddingModelStore>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp = Substitute.For<ILlamaCppLocator>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSettingsViewModelTests"/> class: nothing is chosen, downloaded
    /// or installed yet.
    /// </summary>
    public SearchSettingsViewModelTests()
    {
        _server.Load().Returns(SummaryPreferences.Default);
        _store.Load().Returns(SearchSource.Off);
        _llamaCpp.LocateServer().Returns((string?)null);
    }

    /// <summary>
    /// The remembered choice is shown as the one checked option.
    /// </summary>
    [Fact]
    public void Load_ShowsTheRememberedChoice()
    {
        _store.Load().Returns(SearchSource.Server);

        var search = Search();

        search.IsServer.Should().BeTrue();
        search.IsLocal.Should().BeFalse();
        search.IsOff.Should().BeFalse();
    }

    /// <summary>
    /// Checking an option makes it the choice and remembers it; the option unchecked by it changes nothing.
    /// </summary>
    [Fact]
    public void CheckingAnOption_ChoosesIt()
    {
        var search = Search();

        search.IsLocal = true;
        search.IsOff = false;

        search.Source.Should().Be(SearchSource.Local);
        search.IsOff.Should().BeFalse();
        _store.Received(1).Save(SearchSource.Local);
        _store.DidNotReceive().Save(SearchSource.Off);
    }

    /// <summary>
    /// A choice that does not work yet says why: the model of Voicer is not downloaded, then llama.cpp is not
    /// installed; no model of the server is chosen. Off and a ready choice say nothing.
    /// </summary>
    [Fact]
    public void ChoiceNotReady_SaysWhy()
    {
        var search = Search();
        search.HasWarning.Should().BeFalse();

        search.IsLocal = true;
        search.Warning.Should().Be(TestTexts.Russian.Get("Search.Source.LocalMissing"));

        _models.Preferred().Returns(EmbeddingModelCatalog.BgeM3);
        search.Refresh();
        search.Warning.Should().Be(TestTexts.Russian.Get("Search.Source.LlamaMissing"));

        _llamaCpp.LocateServer().Returns("llama-server");
        search.Refresh();
        search.HasWarning.Should().BeFalse();

        search.IsServer = true;
        search.Warning.Should().Be(TestTexts.Russian.Get("Search.Source.ServerMissing"));

        _server.Load().Returns(SummaryPreferences.Default with { EmbeddingModel = "text-embedding-bge-m3" });
        search.Refresh();
        search.HasWarning.Should().BeFalse();
    }

    /// <summary>
    /// Creates the view model under test, in Russian.
    /// </summary>
    /// <returns>The view model.</returns>
    private SearchSettingsViewModel Search()
    {
        var monitor = Substitute.For<IEmbeddingServerMonitor>();
        monitor.Status().Returns(EmbeddingServerStatus.Stopped);
        var dialogs = Substitute.For<IDialogService>();

        return new SearchSettingsViewModel(
            TestTexts.Russian,
            _store,
            _models,
            _server,
            _llamaCpp,
            new ModelCatalogViewModel(TestTexts.Russian, "Settings.SearchModels", "Models.SearchWindowHint", [], dialogs),
            new SearchServerViewModel(TestTexts.Russian, monitor, TimeProvider.System),
            new SummarySettingsViewModel(TestTexts.Russian, _server, Substitute.For<ILanguageModelClient>(), _store));
    }
}
