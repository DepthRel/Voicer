using FluentAssertions;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Synthesis;
using Voicer.Speech.Llama.Engine;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the arguments of llama-tts.
/// </summary>
public sealed class LlamaTtsCommandLineTests
{
    /// <summary>
    /// The processor.
    /// </summary>
    private static readonly ComputeDevice _cpu = new(ComputeDeviceKind.Cpu, ComputeDevice.CPU_INDEX, "CPU", 0, 0);

    /// <summary>
    /// The second video card of ggml.
    /// </summary>
    private static readonly ComputeDevice _gpu = new(ComputeDeviceKind.Gpu, 1, "RTX", 0, 0);

    /// <summary>
    /// A model of several languages gets its language, the sample and the chosen video card of Vulkan; every value is
    /// an argument of its own, the text too.
    /// </summary>
    [Fact]
    public void Build_PassesTheLanguage_TheSample_AndTheVideoCard()
    {
        var voice = new SynthesisVoice(VoiceModelCatalog.Qwen3Tts, "ru", null, "/tmp/voice.wav", _gpu);

        var arguments = LlamaTtsCommandLine.Build(voice, "/m/w.gguf", "/m/p.gguf", "-Hello; \"world\" & more", "/t/out.wav", isMacOS: false);

        arguments.Should().Equal(
            "-m", "/m/w.gguf",
            "-mm", "/m/p.gguf",
            "-p", "-Hello; \"world\" & more",
            "--output", "/t/out.wav",
            "--tts-lang", "ru",
            "--tts-speaker-file", "/tmp/voice.wav",
            "--device", "Vulkan1");
    }

    /// <summary>
    /// A model of one language is not told it; a shipped voice is passed; the processor keeps everything off the video
    /// cards.
    /// </summary>
    [Fact]
    public void Build_OnTheProcessor_WithAShippedVoice()
    {
        var voice = new SynthesisVoice(VoiceModelCatalog.Kokoro, "en", "am_michael", null, _cpu);

        var arguments = LlamaTtsCommandLine.Build(voice, "w", "p", "Hi.", "o.wav", isMacOS: false);

        arguments.Should().NotContain("--tts-lang");
        arguments.Should().ContainInConsecutiveOrder("--tts-voice", "am_michael");
        arguments.Should().ContainInConsecutiveOrder("--device", "none", "-ngl", "0");
    }

    /// <summary>
    /// On macOS the video card is the one Metal gives: no device is named.
    /// </summary>
    [Fact]
    public void Build_OnMacOS_LeavesTheDeviceToMetal()
    {
        var voice = new SynthesisVoice(VoiceModelCatalog.ChatterboxTurbo, "en", null, null, _gpu);

        LlamaTtsCommandLine.Build(voice, "w", "p", "Hi.", "o.wav", isMacOS: true).Should().NotContain("--device");
    }

    /// <summary>
    /// The model is not asked for what it cannot do: a language, a voice or a sample it does not take.
    /// </summary>
    [Fact]
    public void Build_RefusesWhatTheModelCannotDo()
    {
        var language = () => LlamaTtsCommandLine.Build(new SynthesisVoice(VoiceModelCatalog.Kokoro, "ru", null, null, _cpu), "w", "p", "Hi.", "o.wav", false);
        var voice = () => LlamaTtsCommandLine.Build(new SynthesisVoice(VoiceModelCatalog.Kokoro, "en", "nobody", null, _cpu), "w", "p", "Hi.", "o.wav", false);
        var sample = () => LlamaTtsCommandLine.Build(new SynthesisVoice(VoiceModelCatalog.Kokoro, "en", null, "s.wav", _cpu), "w", "p", "Hi.", "o.wav", false);

        language.Should().Throw<ArgumentException>();
        voice.Should().Throw<ArgumentException>();
        sample.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// The line of the log that explains a failure is the last one about an error, or else the last one.
    /// </summary>
    [Fact]
    public void LastWords_PrefersTheLastError()
    {
        LlamaTtsSynthesizer.LastWords(["load", "error: out of memory", "exit"]).Should().Be("error: out of memory");
        LlamaTtsSynthesizer.LastWords(["load", "exit"]).Should().Be("exit");
        LlamaTtsSynthesizer.LastWords([]).Should().BeEmpty();
    }
}
