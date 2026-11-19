namespace Voicer.Media;

/// <summary>
/// Compresses the sound of an audio or video file to be kept with its text and listened to later.
/// </summary>
public interface IAudioEncoder
{
    /// <summary>
    /// Compresses the first sound track of a file.
    /// </summary>
    /// <param name="source">The media file.</param>
    /// <param name="destination">The Ogg Opus file to write; it is replaced.</param>
    /// <param name="progress">Receives how much is done, from 0 to 100, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>How long the sound lasts.</returns>
    /// <exception cref="FileNotFoundException">FFmpeg or the file is not found.</exception>
    /// <exception cref="InvalidDataException">The file has no sound, or FFmpeg cannot compress it.</exception>
    Task<TimeSpan> EncodeAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken);
}
