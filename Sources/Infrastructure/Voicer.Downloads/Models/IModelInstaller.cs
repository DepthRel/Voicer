using Voicer.Core.Models;

namespace Voicer.Downloads.Models;

/// <summary>
/// Downloads speech models into their folders.
/// </summary>
public interface IModelInstaller
{
    /// <summary>
    /// Downloads a model into <c>Data/Models/&lt;id&gt;</c>; an interrupted download continues where it stopped.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model is installed.</returns>
    Task InstallAsync(SpeechModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
