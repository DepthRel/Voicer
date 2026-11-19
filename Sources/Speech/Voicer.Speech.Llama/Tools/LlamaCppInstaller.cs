using Voicer.Downloads;
using Voicer.Downloads.Enums;
using Voicer.Localization;

namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// <see cref="ILlamaCppInstaller"/> that downloads the archive of <see cref="LlamaCppPackage"/>, unpacks it next to the
/// folder of the build and puts it in place only when it is whole.
/// </summary>
public sealed class LlamaCppInstaller : ILlamaCppInstaller
{
    /// <summary>
    /// The ending of the folder a build is unpacked into before it is put in place.
    /// </summary>
    private const string STAGING_SUFFIX = ".unpacking";

    /// <summary>
    /// The downloader of the archive.
    /// </summary>
    private readonly IFileDownloader _downloader;

    /// <summary>
    /// The locator that says where llama.cpp is installed.
    /// </summary>
    private readonly ILlamaCppLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaCppInstaller"/> class.
    /// </summary>
    /// <param name="downloader">The downloader of the archive.</param>
    /// <param name="locator">The locator that says where llama.cpp is installed.</param>
    /// <param name="build">The build to install, or <see langword="null"/> when there is none for the system.</param>
    public LlamaCppInstaller(IFileDownloader downloader, ILlamaCppLocator locator, LlamaCppBuild? build)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        Build = build;
    }

    /// <inheritdoc />
    public LlamaCppBuild? Build { get; }

    /// <inheritdoc />
    public bool CanInstall => Build is not null;

    /// <inheritdoc />
    public async Task<string> InstallAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        if (Build is not { } build)
        {
            throw LocalizedError.Create(m => new PlatformNotSupportedException(m), "LlamaCpp.InstallUnsupported");
        }

        Directory.CreateDirectory(_locator.RootDirectory);
        var archive = Path.Combine(_locator.RootDirectory, build.ArchiveName);
        var staging = _locator.InstallDirectory + STAGING_SUFFIX;

        var request = new DownloadRequest(build.Source, archive, build.SizeBytes, build.Sha256);
        await _downloader.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report(new DownloadProgress(DownloadStage.Installing, 0, 0, 0));
        try
        {
            await Task.Run(() => Unpack(build, archive, staging), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(archive);
            TryDeleteFolder(staging);
        }

        RemoveOtherBuilds();

        return _locator.Locate() ??
            throw LocalizedError.Create(m => new InvalidDataException(m), "LlamaCpp.ArchiveIncomplete", build.ArchiveName);
    }

    /// <summary>
    /// Unpacks the archive aside and puts the build in place of the one there was.
    /// </summary>
    /// <param name="build">The build.</param>
    /// <param name="archive">The checked archive.</param>
    /// <param name="staging">The folder to unpack into first.</param>
    private void Unpack(LlamaCppBuild build, string archive, string staging)
    {
        TryDeleteFolder(staging);
        if (build.IsZip)
        {
            ArchiveExtractor.ExtractZip(archive, staging);
        }
        else
        {
            ArchiveExtractor.ExtractTarGz(archive, staging);
        }

        if (Directory.Exists(_locator.InstallDirectory))
        {
            Directory.Delete(_locator.InstallDirectory, recursive: true);
        }

        Directory.Move(staging, _locator.InstallDirectory);
    }

    /// <summary>
    /// Removes the builds of other releases, which the application no longer runs.
    /// </summary>
    private void RemoveOtherBuilds()
    {
        var current = Path.GetFullPath(_locator.InstallDirectory);
        foreach (var folder in Directory.EnumerateDirectories(_locator.RootDirectory))
        {
            if (!string.Equals(Path.GetFullPath(folder), current, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFolder(folder);
            }
        }
    }

    /// <summary>
    /// Removes a folder with its files, if it can.
    /// </summary>
    /// <param name="folder">The folder.</param>
    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A program of the old build may still run; its folder goes the next time.
        }
    }
}
