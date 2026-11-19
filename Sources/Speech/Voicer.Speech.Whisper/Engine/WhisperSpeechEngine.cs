using Whisper.net;
using Voicer.Core.Devices;
using Voicer.Core.Transcripts;

namespace Voicer.Speech.Whisper.Engine;

/// <summary>
/// <see cref="ISpeechEngine"/> on whisper.cpp through Whisper.net. The language of the speech is detected by the model.
/// </summary>
public sealed class WhisperSpeechEngine : ISpeechEngine
{
    /// <summary>
    /// The language setting that asks the model to detect the language itself.
    /// </summary>
    private const string AUTO_LANGUAGE = "auto";

    /// <inheritdoc />
    public async Task<string?> TranscribeAsync(
        string modelPath,
        string audioPath,
        int deviceIndex,
        ITranscriptionListener listener,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentNullException.ThrowIfNull(listener);

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
                await using var audio = File.OpenRead(audioPath);
                await foreach (var segment in processor.ProcessAsync(audio, cancellationToken).ConfigureAwait(false))
                {
                    language ??= string.IsNullOrWhiteSpace(segment.Language) ? null : segment.Language;
                    listener.Segment(new TranscriptSegment(segment.Start, segment.End, segment.Text));
                }

                return language;
            }
        }
    }
}
