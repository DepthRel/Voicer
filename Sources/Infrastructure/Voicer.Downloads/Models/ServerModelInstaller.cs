using Voicer.Core.Models;

namespace Voicer.Downloads.Models;

/// <summary>
/// <see cref="IServerModelInstaller{TModel}"/> over the downloader: the size and the SHA-256 of the catalog are checked
/// before the file gets its name.
/// </summary>
/// <typeparam name="TModel">The kind of the models.</typeparam>
public sealed class ServerModelInstaller<TModel> : IServerModelInstaller<TModel>
    where TModel : class, IServerModel
{
    /// <summary>
    /// Downloads the files.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// Where the models live.
    /// </summary>
    private readonly IServerModelStore<TModel> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerModelInstaller{TModel}"/> class.
    /// </summary>
    /// <param name="downloader">Downloads the files.</param>
    /// <param name="store">Where the models live.</param>
    public ServerModelInstaller(IFileDownloader downloader, IServerModelStore<TModel> store)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task InstallAsync(TModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
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
