namespace Voicer.Media;

/// <summary>
/// Extracts the sound of a media file as the WAV the speech engine reads: 16 kHz, mono, 16-bit.
/// </summary>
public interface IAudioExtractor
{
    /// <summary>
    /// Extracts the sound of an audio or video file.
    /// </summary>
    /// <param name="source">The media file.</param>
    /// <param name="destination">The WAV file to write; it is replaced.</param>
    /// <param name="progress">Receives how much is extracted, from 0 to 100, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the extraction.</param>
    /// <returns>The duration of the sound.</returns>
    /// <exception cref="FileNotFoundException">FFmpeg is not installed.</exception>
    /// <exception cref="InvalidDataException">The file has no sound or cannot be read.</exception>
    Task<TimeSpan> ExtractAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken);
}
