using System.Globalization;
using Voicer.Core.Enums;
using Voicer.Core.Synthesis;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.Media;
using Voicer.UI.Enums;

namespace Voicer.UI.Services.Generation;

/// <summary>
/// <see cref="IGenerationRunner"/> that prepares the sample with FFmpeg, speaks the text piece by piece with the
/// synthesizer (the model leaves memory after every piece) and joins the pieces with short pauses. Everything lives in a
/// folder of its own in <c>Data/Temp/Generation</c>, which a failure or a cancellation removes.
/// </summary>
public sealed class GenerationRunner : IGenerationRunner
{
    /// <summary>
    /// The folder of the spoken pieces and the prepared sample inside the folder of a generation.
    /// </summary>
    private const string PARTS_FOLDER = "parts";

    /// <summary>
    /// The pause after a piece that ends in the middle of a paragraph.
    /// </summary>
    private static readonly TimeSpan _sentencePause = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The pause after the last piece of a paragraph.
    /// </summary>
    private static readonly TimeSpan _paragraphPause = TimeSpan.FromMilliseconds(650);

    /// <summary>
    /// Speaks the pieces.
    /// </summary>
    private readonly ISpeechSynthesizer _synthesizer;

    /// <summary>
    /// Prepares the sample of the voice.
    /// </summary>
    private readonly IVoiceSampleExtractor _samples;

    /// <summary>
    /// The folder of the generations (<c>Data/Temp/Generation</c>).
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// The clock of the moment a generation finishes.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationRunner"/> class.
    /// </summary>
    /// <param name="synthesizer">Speaks the pieces.</param>
    /// <param name="samples">Prepares the sample of the voice.</param>
    /// <param name="directory">The folder of the generations (<c>Data/Temp/Generation</c>).</param>
    /// <param name="time">The clock; the system clock by default.</param>
    public GenerationRunner(ISpeechSynthesizer synthesizer, IVoiceSampleExtractor samples, string directory, TimeProvider? time = null)
    {
        _synthesizer = synthesizer ?? throw new ArgumentNullException(nameof(synthesizer));
        _samples = samples ?? throw new ArgumentNullException(nameof(samples));
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<GenerationResult> RunAsync(GenerationRequest request, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parts = SpeechScript.Split(request.Text);
        if (parts.Count == 0)
        {
            throw LocalizedError.Argument(nameof(request), "Generation.NothingToSpeak");
        }

        ClearLeftovers();
        var work = Path.Combine(_directory, Guid.NewGuid().ToString("N"));
        var pieces = Path.Combine(work, PARTS_FOLDER);
        Directory.CreateDirectory(pieces);

        var completed = false;
        try
        {
            var voice = await PrepareVoiceAsync(request.Voice, pieces, parts.Count, progress, cancellationToken);

            var files = new List<string>(parts.Count);
            for (var i = 0; i < parts.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new GenerationProgress(GenerationStage.Speaking, i + 1, parts.Count));

                var file = Path.Combine(pieces, $"part-{(i + 1).ToString("0000", CultureInfo.InvariantCulture)}.wav");
                await _synthesizer.SynthesizeAsync(voice, parts[i].Text, file, cancellationToken);
                files.Add(file);
            }

            progress?.Report(new GenerationProgress(GenerationStage.Joining, parts.Count, parts.Count));
            var output = Path.Combine(work, GeneratedFileName.From(request.Text));
            var pauses = parts.Select(p => p.EndsParagraph ? _paragraphPause : _sentencePause).ToList();
            var spans = await Task.Run(() => WaveJoiner.Join(files, pauses, output), cancellationToken);
            TryDeleteFolder(pieces);

            var segments = parts.Select((p, i) => new TranscriptSegment(spans[i].Start, spans[i].End, p.Text)).ToList();
            var transcript = new Transcript(
                output,
                voice.Model.Name,
                request.DeviceName,
                voice.Language,
                _time.GetLocalNow(),
                segments,
                RecordKind.Generation);

            completed = true;

            return new GenerationResult(output, spans[^1].End, transcript);
        }
        finally
        {
            if (!completed)
            {
                TryDeleteFolder(work);
            }
        }
    }

    /// <inheritdoc />
    public void ClearLeftovers()
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        foreach (var folder in Directory.EnumerateDirectories(_directory))
        {
            TryDeleteFolder(folder);
        }
    }

    /// <summary>
    /// Prepares the sample of the voice, when there is one: any audio or video file becomes a short WAV file.
    /// </summary>
    /// <param name="voice">How the text is spoken, with the sample the user chose.</param>
    /// <param name="folder">The folder to put the prepared sample in.</param>
    /// <param name="parts">How many pieces the text has.</param>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="cancellationToken">Stops the preparation.</param>
    /// <returns>How the text is spoken, with the prepared sample.</returns>
    private async Task<SynthesisVoice> PrepareVoiceAsync(
        SynthesisVoice voice,
        string folder,
        int parts,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (voice.SamplePath is not { } sample)
        {
            return voice;
        }

        progress?.Report(new GenerationProgress(GenerationStage.PreparingVoice, 0, parts));
        var prepared = Path.Combine(folder, "voice.wav");
        await _samples.ExtractAsync(sample, prepared, cancellationToken);

        return voice with { SamplePath = prepared };
    }

    /// <summary>
    /// Removes a folder with its files, if it can.
    /// </summary>
    /// <param name="folder">The folder.</param>
    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in use (a player of the previous result): it goes on the next start.
        }
    }
}
