namespace Voicer.Speech.Whisper.Engine;

/// <summary>
/// Recognizes the speech of a recording with a model on a compute device.
/// </summary>
public interface ISpeechEngine
{
    /// <summary>
    /// Recognizes the speech of a WAV file (16 kHz, mono, 16-bit).
    /// </summary>
    /// <param name="modelPath">The file of the model.</param>
    /// <param name="audioPath">The WAV file.</param>
    /// <param name="deviceIndex">The index of the video card, or <c>-1</c> for the processor.</param>
    /// <param name="listener">Receives the progress and the recognized pieces as they come.</param>
    /// <param name="cancellationToken">Stops the recognition.</param>
    /// <returns>The language the model detected, or <see langword="null"/>.</returns>
    /// <exception cref="ModelLoadException">The model could not be loaded on the device.</exception>
    Task<string?> TranscribeAsync(
        string modelPath,
        string audioPath,
        int deviceIndex,
        ITranscriptionListener listener,
        CancellationToken cancellationToken);
}
