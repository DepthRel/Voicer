using System.Globalization;
using Voicer.Core.Synthesis;

namespace Voicer.Speech.Llama.Engine;

/// <summary>
/// The arguments of llama-tts for one piece of text: the files of the model, the text, the output, the language, the
/// voice and the device. Every argument is a separate item: the program is started without a shell.
/// </summary>
public static class LlamaTtsCommandLine
{
    /// <summary>
    /// Builds the arguments.
    /// </summary>
    /// <param name="voice">How the text is spoken.</param>
    /// <param name="weightsPath">The full path of the weights of the model.</param>
    /// <param name="projectorPath">The full path of the projector of the model.</param>
    /// <param name="text">The text to speak.</param>
    /// <param name="outputPath">The full path of the WAV file to write.</param>
    /// <param name="isMacOS">Whether the system is macOS, where the video card is the one Metal gives.</param>
    /// <returns>The arguments, in their order.</returns>
    /// <exception cref="ArgumentException">The voice asks the model for what it cannot do.</exception>
    public static IReadOnlyList<string> Build(
        SynthesisVoice voice,
        string weightsPath,
        string projectorPath,
        string text,
        string outputPath,
        bool isMacOS)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentException.ThrowIfNullOrWhiteSpace(weightsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectorPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        Check(voice);

        var model = voice.Model;
        var arguments = new List<string>
        {
            "-m", weightsPath,
            "-mm", projectorPath,
            "-p", text,
            "--output", outputPath,
        };

        // A model of one language is not told it: some of them refuse the option.
        if (model.IsMultilingual)
        {
            arguments.AddRange(["--tts-lang", voice.Language]);
        }

        if (voice.Voice is { } preset)
        {
            arguments.AddRange(["--tts-voice", preset]);
        }

        if (voice.SamplePath is { } sample)
        {
            arguments.AddRange(["--tts-speaker-file", sample]);
        }

        // The processor: nothing goes to a video card. A video card: the one chosen, by its place among the video
        // cards of ggml, which is the order of Vulkan; on macOS the single Metal device is taken by itself.
        if (!voice.Device.IsGpu)
        {
            arguments.AddRange(["--device", "none", "-ngl", "0"]);
        }
        else if (!isMacOS)
        {
            arguments.AddRange(["--device", "Vulkan" + voice.Device.Index.ToString(CultureInfo.InvariantCulture)]);
        }

        return arguments;
    }

    /// <summary>
    /// Checks that the voice asks the model only for what it can do.
    /// </summary>
    /// <param name="voice">How the text is spoken.</param>
    private static void Check(SynthesisVoice voice)
    {
        var model = voice.Model;
        if (!model.Languages.Contains(voice.Language, StringComparer.Ordinal))
        {
            throw new ArgumentException($"{model.Name} does not speak '{voice.Language}'.", nameof(voice));
        }

        if (voice.Voice is { } preset && !model.Voices.Contains(preset, StringComparer.Ordinal))
        {
            throw new ArgumentException($"{model.Name} has no voice '{preset}'.", nameof(voice));
        }

        if (voice.SamplePath is not null && !model.CanCloneVoice)
        {
            throw new ArgumentException($"{model.Name} cannot speak in the voice of a sample.", nameof(voice));
        }
    }
}
