using Voicer.Core.Embeddings;

namespace Voicer.Downloads.Models;

/// <summary>
/// <see cref="IEmbeddingModelInstaller"/> over the downloader checked by size and SHA-256.
/// </summary>
public sealed class EmbeddingModelInstaller : IEmbeddingModelInstaller
{
    /// <summary>
    /// Downloads and checks the file.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// Where the models live.
    /// </summary>
    private readonly IEmbeddingModelStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingModelInstaller"/> class.
    /// </summary>
    /// <param name="downloader">Downloads and checks the file.</param>
    /// <param name="store">Where the models live.</param>
    public EmbeddingModelInstaller(IFileDownloader downloader, IEmbeddingModelStore store)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task InstallAsync(EmbeddingModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_store.IsInstalled(model))
        {
            return Task.CompletedTask;
        }

        var request = new DownloadRequest(model.DownloadUri, _store.FileOf(model), model.SizeBytes, model.Sha256);

        return _downloader.DownloadAsync(request, progress, cancellationToken);
    }
}
