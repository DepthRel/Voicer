using Voicer.Core.Models;

namespace Voicer.Downloads.Models;

/// <summary>
/// <see cref="IModelInstaller"/> that downloads the pinned file of a model into the folder the store keeps it in.
/// </summary>
public sealed class ModelInstaller : IModelInstaller
{
    /// <summary>
    /// The downloader of the files.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// The store that says where a model lives.
    /// </summary>
    private readonly IModelStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelInstaller"/> class.
    /// </summary>
    /// <param name="downloader">The downloader of the files.</param>
    /// <param name="store">The store that says where a model lives.</param>
    public ModelInstaller(IFileDownloader downloader, IModelStore store)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task InstallAsync(SpeechModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var request = new DownloadRequest(model.DownloadUri, _store.FileOf(model), model.SizeBytes, model.Sha256);
        await _downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

        // The detector is small and every recognition needs it: it comes with the first model.
        await InstallDetectorAsync(null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InstallDetectorAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var detector = SpeechDetectorModel.SileroVad;
        if (_store.IsDetectorInstalled(detector))
        {
            return Task.CompletedTask;
        }

        var request = new DownloadRequest(detector.DownloadUri, _store.DetectorFileOf(detector), detector.SizeBytes, detector.Sha256);

        return _downloader.DownloadAsync(request, progress, cancellationToken);
    }
}
