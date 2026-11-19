using Voicer.Core.Synthesis;

namespace Voicer.Downloads.Models;

/// <summary>
/// <see cref="IVoiceModelInstaller"/> that downloads the pinned files of a model one after another into the folder the
/// store keeps it in, and reports them as one download.
/// </summary>
public sealed class VoiceModelInstaller : IVoiceModelInstaller
{
    /// <summary>
    /// The downloader of the files.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// The store that says where a model lives.
    /// </summary>
    private readonly IVoiceModelStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModelInstaller"/> class.
    /// </summary>
    /// <param name="downloader">The downloader of the files.</param>
    /// <param name="store">The store that says where a model lives.</param>
    public VoiceModelInstaller(IFileDownloader downloader, IVoiceModelStore store)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task InstallAsync(VoiceModel model, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var before = 0L;
        foreach (var file in model.Files)
        {
            var path = _store.FileOf(model, file);
            var existing = new FileInfo(path);
            if (!existing.Exists || existing.Length != file.SizeBytes)
            {
                var whole = progress is null ? null : new WholeModelProgress(progress, before, model.SizeBytes);
                var request = new DownloadRequest(file.DownloadUri, path, file.SizeBytes, file.Sha256);
                await _downloader.DownloadAsync(request, whole, cancellationToken).ConfigureAwait(false);
            }

            before += file.SizeBytes;
        }
    }

    /// <summary>
    /// Turns the progress of one file into the progress of the whole model: the files before it count as received.
    /// </summary>
    /// <param name="target">Receives the progress of the model.</param>
    /// <param name="before">The bytes of the files before this one.</param>
    /// <param name="total">The bytes of the whole model.</param>
    private sealed class WholeModelProgress(IProgress<DownloadProgress> target, long before, long total) : IProgress<DownloadProgress>
    {
        /// <inheritdoc />
        public void Report(DownloadProgress value)
        {
            target.Report(value with { DoneBytes = before + value.DoneBytes, TotalBytes = total });
        }
    }
}
