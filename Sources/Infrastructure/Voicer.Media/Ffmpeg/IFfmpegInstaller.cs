using Voicer.Downloads;

namespace Voicer.Media.Ffmpeg;

/// <summary>
/// Installs FFmpeg into the tools folder of the application, where the system allows it.
/// </summary>
public interface IFfmpegInstaller
{
    /// <summary>
    /// Gets a value indicating whether the application can install FFmpeg on this system by itself (Windows); elsewhere
    /// the user installs it with the package manager of the system.
    /// </summary>
    bool CanInstall { get; }

    /// <summary>
    /// Downloads the pinned build of FFmpeg, checks it and puts the program into <c>Data/Tools/FFmpeg</c>.
    /// </summary>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the installation.</param>
    /// <returns>The full path of the installed program.</returns>
    /// <exception cref="PlatformNotSupportedException">The system is not one <see cref="CanInstall"/> covers.</exception>
    Task<string> InstallAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
