using FluentAssertions;
using NSubstitute;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Servers;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the choice of the model of the summaries in the settings: off, a model of Voicer, or a model of the server.
/// </summary>
public sealed class SummaryModelSettingsViewModelTests
{
    /// <summary>
    /// Remembers the choice.
    /// </summary>
    private readonly ISummarySourceStore _store = Substitute.For<ISummarySourceStore>();

    /// <summary>
    /// The models of Voicer.
    /// </summary>
    private readonly IChatModelStore _models = Substitute.For<IChatModelStore>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp = Substitute.For<ILlamaCppLocator>();

    /// <summary>
    /// The llama-server of the model of Voicer.
    /// </summary>
    private readonly ILlamaServerMonitor _monitor = Substitute.For<ILlamaServerMonitor>();

    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryModelSettingsViewModelTests"/> class.
    /// </summary>
    public SummaryModelSettingsViewModelTests()
    {
        _server.Load().Returns(SummaryPreferences.Default);
        _store.Load().Returns(ModelSource.Off);
        _llamaCpp.LocateServer().Returns((string?)null);
        _monitor.Status().Returns(LlamaServerStatus.Stopped);
    }

    /// <summary>
    /// A choice that cannot work yet says why: a missing model of Voicer, a missing llama.cpp, or no model of the
    /// server chosen.
    /// </summary>
    [Fact]
    public void ChoiceNotReady_SaysWhy()
    {
        var summaries = Summaries();
        summaries.HasWarning.Should().BeFalse();

        summaries.IsLocal = true;
        summaries.Warning.Should().Be(TestTexts.Russian.Get("Summary.Source.LocalMissing"));
        _store.Received(1).Save(ModelSource.Local);

        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        summaries.Refresh();
        summaries.Warning.Should().Be(TestTexts.Russian.Get("Summary.Source.LlamaMissing"));

        _llamaCpp.LocateServer().Returns("llama-server");
        summaries.Refresh();
        summaries.HasWarning.Should().BeFalse();

        summaries.IsServer = true;
        summaries.Warning.Should().Be(TestTexts.Russian.Get("Summary.Source.ServerMissing"));

        _server.Load().Returns(SummaryPreferences.Default with { Model = "qwen3-8b" });
        summaries.Refresh();
        summaries.HasWarning.Should().BeFalse();
    }

    /// <summary>
    /// Turning the summaries off unloads the model of Voicer at once.
    /// </summary>
    [Fact]
    public void TurningOff_UnloadsTheModelOfVoicer()
    {
        _store.Load().Returns(ModelSource.Local);
        _monitor.Status().Returns(new LlamaServerStatus(LlamaServerState.Idle, "Qwen3-1.7B", null, null));
        var summaries = Summaries();
        summaries.Refresh();

        summaries.IsOff = true;

        _monitor.Received(1).Stop();
        _store.Received(1).Save(ModelSource.Off);
    }

    /// <summary>
    /// Builds the view model under test.
    /// </summary>
    /// <returns>The view model.</returns>
    private SummaryModelSettingsViewModel Summaries()
    {
        var dialogs = Substitute.For<IDialogService>();

        return new SummaryModelSettingsViewModel(
            TestTexts.Russian,
            _store,
            _models,
            _server,
            _llamaCpp,
            new ModelCatalogViewModel(TestTexts.Russian, "Settings.SummaryModels", "Models.SummaryWindowHint", [], dialogs),
            new LlamaServerViewModel(TestTexts.Russian, _monitor, TimeProvider.System),
            new LanguageServerSettingsViewModel(TestTexts.Russian, _server, Substitute.For<ILanguageModelClient>(), Substitute.For<ISearchSourceStore>()));
    }
}
