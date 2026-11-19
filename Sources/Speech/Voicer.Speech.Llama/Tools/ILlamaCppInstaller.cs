using Voicer.Downloads;

namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// Installs the pinned build of llama.cpp into the tools folder of the application.
/// </summary>
public interface ILlamaCppInstaller
{
    /// <summary>
    /// Gets the build for this system, or <see langword="null"/> when llama.cpp publishes none for it.
    /// </summary>
    LlamaCppBuild? Build { get; }

    /// <summary>
    /// Gets a value indicating whether there is a build for this system.
    /// </summary>
    bool CanInstall { get; }

    /// <summary>
    /// Downloads the build, checks it and unpacks it into <c>Data/Tools/LlamaCpp/&lt;version&gt;</c>; older builds are
    /// removed.
    /// </summary>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the installation.</param>
    /// <returns>The full path of the installed llama-tts.</returns>
    /// <exception cref="PlatformNotSupportedException">There is no build for this system.</exception>
    /// <exception cref="InvalidDataException">The archive is damaged or does not hold the program.</exception>
    Task<string> InstallAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
