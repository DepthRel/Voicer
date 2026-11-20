using Voicer.Core.Embeddings;

namespace Voicer.Downloads.Models;

/// <summary>
/// Downloads models of embeddings into their folders.
/// </summary>
public interface IEmbeddingModelInstaller
{
    /// <summary>
    /// Downloads the file of a model into <c>Data/Models/&lt;id&gt;</c>; an interrupted download continues where it
    /// stopped, and the file gets its name only after its SHA-256 is checked.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model is installed.</returns>
    Task InstallAsync(EmbeddingModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
