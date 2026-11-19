using Whisper.net;
using Whisper.net.Wave;
using Voicer.Core.Devices;
using Voicer.Core.Transcripts;

namespace Voicer.Speech.Whisper.Engine;

/// <summary>
/// <see cref="ISpeechEngine"/> on whisper.cpp through Whisper.net. The language of the speech is detected by the model.
/// </summary>
/// <remarks>
/// Every window of 30 seconds is decoded on its own: whisper.cpp would otherwise give each window the text of the
/// previous ones as a prompt, and a sentence the model once repeated would come back in every window to the end of a
/// long file (<c>no_context</c> clears that text only once, before the first window). The windows are decoded by beam
/// search, as <c>whisper-cli</c> does, with the fallback to a higher temperature when a window comes out repetitive or
/// unsure; a <see cref="RepetitionGuard"/> leaves out what still loops. With a detector of speech (Silero VAD) only the
/// stretches where someone speaks are joined and recognized (<see cref="SpeechMap"/>), and the times of the text are
/// turned back into those of the file: silence, music and noise never reach the model.
/// </remarks>
public sealed class WhisperSpeechEngine : ISpeechEngine
{
    /// <summary>
    /// The language setting that asks the model to detect the language itself.
    /// </summary>
    private const string AUTO_LANGUAGE = "auto";

    /// <summary>
    /// How many tokens of the text decoded so far are given to the next window: none.
    /// </summary>
    private const int PAST_TEXT_TOKENS = 0;

    /// <summary>
    /// How many hypotheses the beam search keeps, as <c>whisper-cli</c> does by default.
    /// </summary>
    private const int BEAM_SIZE = 5;

    /// <summary>
    /// The temperature of the first decoding of a window: the most likely text.
    /// </summary>
    private const float TEMPERATURE = 0f;

    /// <summary>
    /// How much the temperature rises for each new decoding of a window that came out repetitive or unsure.
    /// </summary>
    private const float TEMPERATURE_STEP = 0.2f;

    /// <summary>
    /// The entropy of the tokens below which a window counts as repetitive and is decoded again.
    /// </summary>
    private const float ENTROPY_THRESHOLD = 2.4f;

    /// <summary>
    /// The average log probability below which a window counts as unsure and is decoded again.
    /// </summary>
    private const float LOG_PROB_THRESHOLD = -1f;

    /// <summary>
    /// The probability of silence above which an unsure window is taken as no speech.
    /// </summary>
    private const float NO_SPEECH_THRESHOLD = 0.6f;

    /// <summary>
    /// The sample rate of the WAV files the worker gets.
    /// </summary>
    private const int SAMPLE_RATE = 16000;

    /// <summary>
    /// The probability above which the detector takes a moment as speech.
    /// </summary>
    private const float SPEECH_THRESHOLD = 0.5f;

    /// <summary>
    /// The shortest stretch the detector takes as speech: a click or a cough is not.
    /// </summary>
    private static readonly TimeSpan _minSpeech = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The shortest silence that ends a stretch of speech.
    /// </summary>
    private static readonly TimeSpan _minSilence = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How much sound around a stretch of speech is kept, so the first and the last sounds of a word are not cut.
    /// </summary>
    private static readonly TimeSpan _speechPadding = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// The pause put between two stretches of speech in the sound the model hears.
    /// </summary>
    private static readonly TimeSpan _pause = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc />
    public async Task<string?> TranscribeAsync(
        string modelPath,
        string audioPath,
        int deviceIndex,
        string? detectorPath,
        ITranscriptionListener listener,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentNullException.ThrowIfNull(listener);

        float[] samples;
        await using (var audio = File.OpenRead(audioPath))
        {
            samples = await new WaveParser(audio).GetAvgSamplesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Only the speech reaches the model: silence, music and noise would make it invent text.
        var map = detectorPath is null ? null : DetectSpeech(detectorPath, samples);
        if (map is { IsEmpty: true })
        {
            listener.ModelLoaded();

            return null;
        }

        if (map is not null)
        {
            samples = map.Compact(samples);
        }

        var options = new WhisperFactoryOptions
        {
            UseGpu = deviceIndex != ComputeDevice.CPU_INDEX,
            GpuDevice = Math.Max(deviceIndex, 0),
        };

        WhisperFactory factory;
        WhisperProcessor processor;
        try
        {
            factory = WhisperFactory.FromPath(modelPath, options);
            processor = factory.CreateBuilder()
                .WithLanguage(AUTO_LANGUAGE)
                .WithMaxLastTextTokens(PAST_TEXT_TOKENS)
                .WithTemperature(TEMPERATURE)
                .WithTemperatureInc(TEMPERATURE_STEP)
                .WithEntropyThreshold(ENTROPY_THRESHOLD)
                .WithLogProbThreshold(LOG_PROB_THRESHOLD)
                .WithNoSpeechThreshold(NO_SPEECH_THRESHOLD)
                .WithBeamSearchSamplingStrategy(beam => beam.WithBeamSize(BEAM_SIZE))
                .WithProgressHandler(percent => listener.Progress(percent))
                .Build();
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            throw new ModelLoadException($"The model '{modelPath}' could not be loaded on device {deviceIndex}: {ex.Message}", ex);
        }

        using (factory)
        {
            await using (processor)
            {
                listener.ModelLoaded();

                string? language = null;
                var guard = new RepetitionGuard();
                await foreach (var segment in processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
                {
                    language ??= string.IsNullOrWhiteSpace(segment.Language) ? null : segment.Language;
                    var piece = map is null
                        ? new TranscriptSegment(segment.Start, segment.End, segment.Text)
                        : new TranscriptSegment(map.ToOriginal(segment.Start), map.ToOriginal(segment.End), segment.Text);
                    if (guard.Accept(piece))
                    {
                        listener.Segment(piece);
                    }
                }

                return language;
            }
        }
    }

    /// <summary>
    /// Finds where someone speaks, on the processor: the detector is small and fast, and it never touches the video
    /// card the model will need.
    /// </summary>
    /// <param name="detectorPath">The file of the detector.</param>
    /// <param name="samples">The samples of the sound, 16 kHz mono.</param>
    /// <returns>The map of the speech of the sound.</returns>
    /// <exception cref="ModelLoadException">The detector could not be loaded.</exception>
    private static SpeechMap DetectSpeech(string detectorPath, float[] samples)
    {
        IReadOnlyList<VadSegmentData> speech;
        try
        {
            using var factory = WhisperVadFactory.FromPath(detectorPath);
            using var detector = factory.CreateBuilder()
                .WithUseGpu(false)
                .WithThreshold(SPEECH_THRESHOLD)
                .WithMinSpeechDuration(_minSpeech)
                .WithMinSilenceDuration(_minSilence)
                .WithSpeechPadding(_speechPadding)
                .Build();
            speech = detector.DetectSpeech(samples);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            throw new ModelLoadException($"The detector of speech '{detectorPath}' could not be loaded: {ex.Message}", ex);
        }

        return SpeechMap.Create([.. speech.Select(s => new SpeechSpan(s.Start, s.End))], samples.Length, SAMPLE_RATE, _pause);
    }
}
