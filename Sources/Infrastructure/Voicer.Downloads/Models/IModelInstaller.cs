using Voicer.Core.Models;

namespace Voicer.Downloads.Models;

/// <summary>
/// Downloads speech models into their folders.
/// </summary>
public interface IModelInstaller
{
    /// <summary>
    /// Downloads a model into <c>Data/Models/&lt;id&gt;</c>, and the detector of speech every recognition uses when it is
    /// not there yet; an interrupted download continues where it stopped.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="progress">Receives the progress of the model, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model and the detector are installed.</returns>
    Task InstallAsync(SpeechModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the detector of speech, <see cref="SpeechDetectorModel.SileroVad"/>, when it is not there yet.
    /// </summary>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the detector is installed.</returns>
    Task InstallDetectorAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
