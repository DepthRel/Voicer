using FluentAssertions;
using NSubstitute;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Playback;
using Voicer.Core.Synthesis;
using Voicer.Core.Transcripts;
using Voicer.Media.Ffmpeg;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.Generation;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the Generation tab: when the generation can start, the choices of a model, and the result.
/// </summary>
public sealed class GenerationViewModelTests
{
    /// <summary>
    /// The store of the voice models.
    /// </summary>
    private readonly IVoiceModelStore _store = Substitute.For<IVoiceModelStore>();

    /// <summary>
    /// The worker that lists the devices.
    /// </summary>
    private readonly IWorkerClient _worker = Substitute.For<IWorkerClient>();

    /// <summary>
    /// The runner of the generation.
    /// </summary>
    private readonly IGenerationRunner _runner = Substitute.For<IGenerationRunner>();

    /// <summary>
    /// Keeps the generated speech in the history.
    /// </summary>
    private readonly IRecordKeeper _keeper = Substitute.For<IRecordKeeper>();

    /// <summary>
    /// The locator of llama.cpp.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp = Substitute.For<ILlamaCppLocator>();

    /// <summary>
    /// Keeps the choices of the page.
    /// </summary>
    private readonly IGenerationPreferencesStore _preferences = Substitute.For<IGenerationPreferencesStore>();

    /// <summary>
    /// The player of the result.
    /// </summary>
    private readonly IAudioPlayer _player = Substitute.For<IAudioPlayer>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationViewModelTests"/> class.
    /// </summary>
    public GenerationViewModelTests()
    {
        _store.Installed().Returns([VoiceModelCatalog.Qwen3Tts, VoiceModelCatalog.Kokoro]);
        _store.IsInstalled(default!).ReturnsForAnyArgs(true);
        _worker.ListDevicesAsync(default).ReturnsForAnyArgs([new ComputeDevice(ComputeDeviceKind.Cpu, -1, "CPU", 1, 2)]);
        _preferences.Load().Returns(GenerationPreferences.Empty);
        _llamaCpp.Locate().Returns("/tools/llama-tts");
    }

    /// <summary>
    /// Generate is available only with a text that is not only spaces.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Generate_NeedsAText()
    {
        var page = await ReadyPage();

        page.GenerateCommand.CanExecute(null).Should().BeFalse();
        page.Text = "   \n\t ";
        page.GenerateCommand.CanExecute(null).Should().BeFalse();
        page.Text = "Hello.";
        page.GenerateCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// A model offers its own languages and voices; the remembered ones are chosen again.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Model_OffersItsLanguagesAndVoices_AndTheRememberedOnes()
    {
        _preferences.Load().Returns(new GenerationPreferences(VoiceModelCatalog.Kokoro.Id, "en", "bm_george", null));
        var page = await ReadyPage();

        page.SelectedModel.Should().Be(VoiceModelCatalog.Kokoro);
        page.HasLanguageChoice.Should().BeFalse();
        page.HasVoices.Should().BeTrue();
        page.SelectedVoice!.Id.Should().Be("bm_george");
        page.SelectedVoice.Title.Should().Be("George · British English, male");
        page.CanUseSample.Should().BeFalse();

        page.SelectedModel = VoiceModelCatalog.Qwen3Tts;

        page.HasLanguageChoice.Should().BeTrue();
        page.SelectedLanguage!.Code.Should().Be("en");
        page.HasVoices.Should().BeFalse();
        page.CanUseSample.Should().BeTrue();
        page.ShowSampleHint.Should().BeTrue("Qwen3-TTS speaks well only with a sample");
    }

    /// <summary>
    /// A generation gives the result to the player, keeps it in the history and remembers the choices.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Generate_OpensThePlayer_AndKeepsTheRecord()
    {
        var transcript = new Transcript("/tmp/Hello.wav", "Kokoro 82M", "CPU", "en", DateTimeOffset.UnixEpoch, [], RecordKind.Generation);
        _runner.RunAsync(default!, default, default).ReturnsForAnyArgs(new GenerationResult("/tmp/Hello.wav", TimeSpan.FromSeconds(3), transcript));
        _keeper.KeepAsync(default!, default, default).ReturnsForAnyArgs(new KeptRecord(7, true, null));
        var page = await ReadyPage();
        page.SelectedModel = VoiceModelCatalog.Kokoro;
        page.Text = "Hello.";

        await page.GenerateCommand.ExecuteAsync(null);

        page.HasError.Should().BeFalse();
        page.HasResult.Should().BeTrue();
        page.HasPlayer.Should().BeTrue();
        page.IsBusy.Should().BeFalse();
        await _runner.Received(1).RunAsync(
            Arg.Is<GenerationRequest>(r => r.Text == "Hello." && r.Voice.Model == VoiceModelCatalog.Kokoro && r.Voice.Voice == "af_heart" && r.Voice.SamplePath == null),
            Arg.Any<IProgress<GenerationProgress>?>(),
            Arg.Any<CancellationToken>());
        await _keeper.Received(1).KeepAsync(transcript, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());
        _preferences.Received(1).Save(new GenerationPreferences(VoiceModelCatalog.Kokoro.Id, "en", "af_heart", null));
    }

    /// <summary>
    /// Without llama.cpp nothing runs: the page says so and offers the settings.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Generate_WithoutTheEngine_SendsToTheSettings()
    {
        _llamaCpp.Locate().Returns((string?)null);
        var page = await ReadyPage();
        page.Text = "Hello.";

        await page.GenerateCommand.ExecuteAsync(null);

        page.ErrorMessage.Should().Be(TestTexts.English.Get("Generation.EngineMissing"));
        page.CanFixInSettings.Should().BeTrue();
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default, default);
    }

    /// <summary>
    /// Creates the page with its models and devices found.
    /// </summary>
    /// <returns>The page.</returns>
    private async Task<GenerationViewModel> ReadyPage()
    {
        var page = new GenerationViewModel(
            TestTexts.English,
            _store,
            _worker,
            _runner,
            Substitute.For<IDialogService>(),
            _keeper,
            Substitute.For<IFfmpegLocator>(),
            _llamaCpp,
            _preferences,
            (_, _) => _player,
            () => { });

        page.RefreshModels();
        await page.LoadDevicesAsync();

        return page;
    }
}
