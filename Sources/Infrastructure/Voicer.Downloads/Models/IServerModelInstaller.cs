using Voicer.Core.Models;

namespace Voicer.Downloads.Models;

/// <summary>
/// Downloads models run by llama-server into their folders.
/// </summary>
/// <typeparam name="TModel">The kind of the models.</typeparam>
public interface IServerModelInstaller<in TModel>
    where TModel : class, IServerModel
{
    /// <summary>
    /// Downloads the file of a model into <c>Data/Models/&lt;id&gt;</c>; an interrupted download continues where it
    /// stopped, and the file gets its name only after its SHA-256 is checked.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model is installed.</returns>
    Task InstallAsync(TModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
