namespace Voicer.Downloads;

/// <summary>
/// A file to download and what it must be.
/// </summary>
/// <param name="Source">The HTTPS address of the file.</param>
/// <param name="Destination">The full path the checked file is put at.</param>
/// <param name="SizeBytes">The exact size of the file, in bytes.</param>
/// <param name="Sha256">The SHA-256 of the file, in hexadecimal.</param>
public sealed record DownloadRequest(Uri Source, string Destination, long SizeBytes, string Sha256);
