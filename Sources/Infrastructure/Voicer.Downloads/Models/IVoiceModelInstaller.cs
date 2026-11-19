using Voicer.Core.Synthesis;

namespace Voicer.Downloads.Models;

/// <summary>
/// Downloads voice models into their folders.
/// </summary>
public interface IVoiceModelInstaller
{
    /// <summary>
    /// Downloads every file of a model into <c>Data/Models/&lt;id&gt;</c>; a file already there is kept, an interrupted
    /// download continues where it stopped.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="progress">Receives the progress of the whole model, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model is installed.</returns>
    Task InstallAsync(VoiceModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
