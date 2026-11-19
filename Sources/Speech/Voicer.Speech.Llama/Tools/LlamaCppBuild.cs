namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// An archive of a released build of llama.cpp for one system, pinned by its size and SHA-256.
/// </summary>
/// <param name="ArchiveName">The name of the archive among the files of the release.</param>
/// <param name="SizeBytes">The exact size of the archive, in bytes.</param>
/// <param name="Sha256">The SHA-256 of the archive, in lower-case hexadecimal.</param>
public sealed record LlamaCppBuild(string ArchiveName, long SizeBytes, string Sha256)
{
    /// <summary>
    /// Gets the address of the archive.
    /// </summary>
    public Uri Source => new($"https://github.com/ggml-org/llama.cpp/releases/download/{LlamaCppPackage.VERSION}/{ArchiveName}");

    /// <summary>
    /// Gets a value indicating whether the archive is a ZIP file; otherwise it is a gzipped tar.
    /// </summary>
    public bool IsZip => ArchiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
}
