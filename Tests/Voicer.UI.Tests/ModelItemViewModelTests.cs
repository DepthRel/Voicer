using FluentAssertions;
using NSubstitute;
using Voicer.Core.Enums;
using Voicer.Core.Models;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of a model family in the settings: the choice of the quantization and its download.
/// </summary>
public sealed class ModelItemViewModelTests
{
    /// <summary>
    /// The store of the models.
    /// </summary>
    private readonly IModelStore _store = Substitute.For<IModelStore>();

    /// <summary>
    /// The installer of the models.
    /// </summary>
    private readonly IModelInstaller _installer = Substitute.For<IModelInstaller>();

    /// <summary>
    /// The files of Whisper large-v3-turbo: full precision, Q8 and Q5.
    /// </summary>
    private readonly IReadOnlyList<SpeechModel> _turbo = [.. SpeechModelCatalog.All.Where(m => m.Family == "whisper-large-v3-turbo")];

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelItemViewModelTests"/> class.
    /// </summary>
    public ModelItemViewModelTests()
    {
        // A file that is never there, so no interrupted download is found.
        _store.FileOf(default!).ReturnsForAnyArgs(Path.Combine(Path.GetTempPath(), "voicer-absent-" + Guid.NewGuid().ToString("N"), "model.bin"));
    }

    /// <summary>
    /// With nothing downloaded, the most accurate quantization is offered, every quantization of the family is listed and
    /// the card says nothing about its state.
    /// </summary>
    [Fact]
    public void Offers_TheMostAccurateQuantization_WhenNothingIsDownloaded()
    {
        var item = Item();

        item.Variants.Select(v => v.Model.Quantization).Should().Equal(ModelQuantization.F16, ModelQuantization.Q8, ModelQuantization.Q5);
        item.Model.Quantization.Should().Be(ModelQuantization.F16);
        item.IsInstalled.Should().BeFalse();
        item.DownloadCommand.CanExecute(null).Should().BeTrue();
        item.StatusText.Should().BeEmpty("a model that is not here says nothing about it");
        item.HasStatus.Should().BeFalse();
    }

    /// <summary>
    /// A quantization that is already downloaded is shown first, as installed.
    /// </summary>
    [Fact]
    public void Shows_TheDownloadedQuantization()
    {
        _store.IsInstalled(_turbo[2]).Returns(true);

        var item = Item();

        item.Model.Should().Be(_turbo[2]);
        item.IsInstalled.Should().BeTrue();
        item.DownloadCommand.CanExecute(null).Should().BeFalse();
        item.StatusText.Should().Be(TestTexts.Russian.Get("Models.Installed"));
    }

    /// <summary>
    /// Choosing another quantization shows its own state, size and description.
    /// </summary>
    [Fact]
    public void ChoosingAQuantization_ShowsItsState()
    {
        _store.IsInstalled(_turbo[0]).Returns(true);
        var item = Item();

        item.SelectedVariant = item.Variants[1];

        item.IsInstalled.Should().BeFalse();
        item.DownloadCommand.CanExecute(null).Should().BeTrue();
        item.SizeText.Should().Be(item.Variants[1].SizeText);
        item.QuantizationDescription.Should().Be(TestTexts.Russian.Get("Quantization.Q8.Description"));
    }

    /// <summary>
    /// The download takes the chosen quantization, marks it installed and tells the page of the recognition.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_TakesTheChosenQuantization()
    {
        var item = Item();
        var installed = 0;
        item.Installed += (_, _) => installed++;
        item.SelectedVariant = item.Variants[2];
        _installer.InstallAsync(_turbo[2], Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _store.IsInstalled(_turbo[2]).Returns(true));

        await item.DownloadCommand.ExecuteAsync(null);

        await _installer.Received(1).InstallAsync(_turbo[2], Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>());
        item.IsInstalled.Should().BeTrue();
        item.Variants[2].IsInstalled.Should().BeTrue();
        item.Variants[0].IsInstalled.Should().BeFalse();
        installed.Should().Be(1);
    }

    /// <summary>
    /// The variants must belong to one family: a mixed list is a mistake of the caller.
    /// </summary>
    [Fact]
    public void Refuses_VariantsOfDifferentFamilies()
    {
        var act = () => new ModelItemViewModel(
            TestTexts.Russian,
            [_turbo[0], SpeechModelCatalog.WhisperLargeV3],
            _store,
            _installer,
            Substitute.For<IDialogService>());

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Creates the model family under test.
    /// </summary>
    /// <returns>The view model of Whisper large-v3-turbo.</returns>
    private ModelItemViewModel Item()
    {
        return new ModelItemViewModel(TestTexts.Russian, _turbo, _store, _installer, Substitute.For<IDialogService>());
    }
}
