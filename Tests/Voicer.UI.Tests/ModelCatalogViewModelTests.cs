using FluentAssertions;
using NSubstitute;
using Voicer.Core.Synthesis;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of a catalog of models: the line of the settings, its window and the cards of the voice models.
/// </summary>
public sealed class ModelCatalogViewModelTests
{
    /// <summary>
    /// The store of the voice models.
    /// </summary>
    private readonly IVoiceModelStore _store = Substitute.For<IVoiceModelStore>();

    /// <summary>
    /// The installer of the voice models.
    /// </summary>
    private readonly IVoiceModelInstaller _installer = Substitute.For<IVoiceModelInstaller>();

    /// <summary>
    /// The dialogs.
    /// </summary>
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCatalogViewModelTests"/> class.
    /// </summary>
    public ModelCatalogViewModelTests()
    {
        // Files that are never there, so no interrupted download is found.
        var absent = Path.Combine(Path.GetTempPath(), "voicer-absent-" + Guid.NewGuid().ToString("N"));
        _store.FileOf(default!, default!).ReturnsForAnyArgs(call => Path.Combine(absent, call.ArgAt<VoiceModelFile>(1).FileName));
        _store.IsInstalled(VoiceModelCatalog.Kokoro).Returns(true);
    }

    /// <summary>
    /// The settings show how many models are downloaded; the window opens the catalog itself.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summary_CountsTheDownloadedModels_AndOpenShowsTheWindow()
    {
        var catalog = Catalog();

        catalog.Summary.Should().Be(TestTexts.Russian.Format("Models.Summary", 1, VoiceModelCatalog.All.Count));
        catalog.HasInstalled.Should().BeTrue();

        await catalog.OpenCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ShowModelsAsync(catalog);
    }

    /// <summary>
    /// A downloaded model is marked as received; one that is not here says nothing; the processor and the voice of a
    /// sample are shown as advantages.
    /// </summary>
    [Fact]
    public void Cards_MarkWhatIsReceived_AndTheAdvantages()
    {
        var cards = Catalog().Models.Cast<VoiceModelItemViewModel>().ToDictionary(c => c.Model);

        cards[VoiceModelCatalog.Kokoro].IsInstalled.Should().BeTrue();
        cards[VoiceModelCatalog.Kokoro].StatusText.Should().Be(TestTexts.Russian.Get("Models.Installed"));
        cards[VoiceModelCatalog.Kokoro].RunsOnCpu.Should().BeTrue();
        cards[VoiceModelCatalog.Qwen3Tts].IsInstalled.Should().BeFalse();
        cards[VoiceModelCatalog.Qwen3Tts].HasStatus.Should().BeFalse();
        cards[VoiceModelCatalog.Qwen3Tts].CanCloneVoice.Should().BeTrue();
        cards[VoiceModelCatalog.Qwen3Tts].DownloadCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A model downloaded from the window changes the line of the settings and tells the pages.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_UpdatesTheSummary_AndTellsThePages()
    {
        var catalog = Catalog();
        var installed = 0;
        catalog.Installed += (_, _) => installed++;
        var qwen = catalog.Models.Cast<VoiceModelItemViewModel>().Single(c => c.Model == VoiceModelCatalog.Qwen3Tts);
        _installer.InstallAsync(VoiceModelCatalog.Qwen3Tts, Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _store.IsInstalled(VoiceModelCatalog.Qwen3Tts).Returns(true);

                return Task.CompletedTask;
            });

        await qwen.DownloadCommand.ExecuteAsync(null);

        qwen.IsInstalled.Should().BeTrue();
        installed.Should().Be(1);
        catalog.Summary.Should().Be(TestTexts.Russian.Format("Models.Summary", 2, VoiceModelCatalog.All.Count));
    }

    /// <summary>
    /// Creates the catalog of the voice models.
    /// </summary>
    /// <returns>The catalog.</returns>
    private ModelCatalogViewModel Catalog()
    {
        return new ModelCatalogViewModel(
            TestTexts.Russian,
            "Settings.VoiceModels",
            "Models.VoiceWindowHint",
            [.. VoiceModelCatalog.All.Select(m => new VoiceModelItemViewModel(TestTexts.Russian, m, _store, _installer, _dialogs))],
            _dialogs);
    }
}
