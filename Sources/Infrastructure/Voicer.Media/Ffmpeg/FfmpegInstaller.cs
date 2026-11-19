using System.IO.Compression;
using Voicer.Downloads;
using Voicer.Downloads.Enums;
using Voicer.Localization;

namespace Voicer.Media.Ffmpeg;

/// <summary>
/// <see cref="IFfmpegInstaller"/> that downloads the archive of <see cref="FfmpegPackage"/> and takes the program and its
/// license out of it.
/// </summary>
public sealed class FfmpegInstaller : IFfmpegInstaller
{
    /// <summary>
    /// The name the archive is downloaded under.
    /// </summary>
    private const string ARCHIVE_NAME = "ffmpeg.zip";

    /// <summary>
    /// The downloader of the archive.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// The locator that says where FFmpeg is installed.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// Whether the system is Windows.
    /// </summary>
    private readonly bool _isWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegInstaller"/> class.
    /// </summary>
    /// <param name="downloader">The downloader of the archive.</param>
    /// <param name="locator">The locator that says where FFmpeg is installed.</param>
    /// <param name="isWindows">Whether the system is Windows; the system it runs on by default.</param>
    public FfmpegInstaller(IFileDownloader downloader, IFfmpegLocator locator, bool? isWindows = null)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _isWindows = isWindows ?? OperatingSystem.IsWindows();
    }

    /// <inheritdoc />
    public bool CanInstall => _isWindows;

    /// <inheritdoc />
    public async Task<string> InstallAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (!CanInstall)
        {
            throw LocalizedError.Create(m => new PlatformNotSupportedException(m), "Ffmpeg.InstallUnsupported");
        }

        var folder = _locator.InstallDirectory;
        var archive = Path.Combine(folder, ARCHIVE_NAME);
        var request = new DownloadRequest(FfmpegPackage.Source, archive, FfmpegPackage.SIZE_BYTES, FfmpegPackage.SHA256);
        await _downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report(new DownloadProgress(DownloadStage.Installing, 0, 0, 0));
        try
        {
            Extract(archive, folder);
        }
        finally
        {
            File.Delete(archive);
        }

        return Path.Combine(folder, FfmpegPackage.Files[0].Name);
    }

    /// <summary>
    /// Takes the files of <see cref="FfmpegPackage.Files"/> out of the archive. The names in the archive are never used
    /// as paths: every file is written under the name the package gives it.
    /// </summary>
    /// <param name="archive">The checked archive.</param>
    /// <param name="folder">The folder of FFmpeg.</param>
    private static void Extract(string archive, string folder)
    {
        using var zip = ZipFile.OpenRead(archive);
        foreach (var (entryName, name) in FfmpegPackage.Files)
        {
            var entry = zip.GetEntry(entryName) ??
                throw LocalizedError.Create(m => new InvalidDataException(m), "Ffmpeg.ArchiveIncomplete", entryName);

            var target = Path.Combine(folder, name);
            var temporary = target + ".tmp";
            entry.ExtractToFile(temporary, overwrite: true);
            File.Move(temporary, target, overwrite: true);
        }
    }
}
