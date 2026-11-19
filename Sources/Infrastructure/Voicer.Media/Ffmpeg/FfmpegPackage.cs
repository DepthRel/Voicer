namespace Voicer.Media.Ffmpeg;

/// <summary>
/// The build of FFmpeg the application installs on Windows: the "essentials" build of Gyan Doshi (GPL-3.0), pinned by its
/// size and SHA-256. Linux and macOS take FFmpeg from their package managers.
/// </summary>
public static class FfmpegPackage
{
    /// <summary>
    /// The version of FFmpeg in the archive.
    /// </summary>
    public const string VERSION = "9.0.2";

    /// <summary>
    /// The exact size of the archive, in bytes.
    /// </summary>
    public const long SIZE_BYTES = 114_768_076;

    /// <summary>
    /// The SHA-256 of the archive.
    /// </summary>
    public const string SHA256 = "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";

    /// <summary>
    /// The folder of the archive that holds the build.
    /// </summary>
    public const string ARCHIVE_ROOT = "ffmpeg-" + VERSION + "-essentials_build/";

    /// <summary>
    /// Gets the address of the archive.
    /// </summary>
    public static Uri Source { get; } =
        new($"https://github.com/GyanD/codexffmpeg/releases/download/{VERSION}/ffmpeg-{VERSION}-essentials_build.zip");

    /// <summary>
    /// Gets the files taken from the archive: the program and its license, by their path in the archive and the name
    /// they get.
    /// </summary>
    public static IReadOnlyList<(string Entry, string Name)> Files { get; } =
    [
        (ARCHIVE_ROOT + "bin/ffmpeg.exe", "ffmpeg.exe"),
        (ARCHIVE_ROOT + "LICENSE", "LICENSE.txt"),
    ];
}
