namespace Voicer.Media.Ffmpeg;

/// <summary>
/// Finds the FFmpeg program.
/// </summary>
public interface IFfmpegLocator
{
    /// <summary>
    /// Gets the folder the application installs FFmpeg into (<c>Data/Tools/FFmpeg</c>).
    /// </summary>
    string InstallDirectory { get; }

    /// <summary>
    /// Finds FFmpeg: the copy the application installed first, then the program of the system.
    /// </summary>
    /// <returns>The full path of the program, or <see langword="null"/> when there is none.</returns>
    string? Locate();
}
