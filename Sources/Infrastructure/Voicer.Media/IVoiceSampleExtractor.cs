namespace Voicer.Media;

/// <summary>
/// Prepares a sample of a voice for a voice model: the beginning of the sound of any audio or video file as a WAV file.
/// </summary>
public interface IVoiceSampleExtractor
{
    /// <summary>
    /// Takes the beginning of the sound of a file.
    /// </summary>
    /// <param name="source">The audio or video file with the voice.</param>
    /// <param name="destination">The WAV file to write; it is replaced.</param>
    /// <param name="cancellationToken">Stops the extraction.</param>
    /// <returns>A task that completes when the sample is written.</returns>
    /// <exception cref="FileNotFoundException">FFmpeg or the source is not found.</exception>
    /// <exception cref="InvalidDataException">The file has no sound or cannot be read.</exception>
    Task ExtractAsync(string source, string destination, CancellationToken cancellationToken);
}
