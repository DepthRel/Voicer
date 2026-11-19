namespace Voicer.Downloads;

/// <summary>
/// Downloads a large file and puts it in place only when it is exactly the expected file.
/// </summary>
public interface IFileDownloader
{
    /// <summary>
    /// Downloads a file. A download that was interrupted continues from where it stopped (the received part waits in
    /// <c>&lt;destination&gt;.part</c>); the finished file replaces the destination only after its size and SHA-256 match.
    /// </summary>
    /// <param name="request">The file and what it must be.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download; the received part stays for the next attempt.</param>
    /// <returns>A task that completes when the file is in place.</returns>
    /// <exception cref="HttpRequestException">The server refused the file or the connection failed.</exception>
    /// <exception cref="InvalidDataException">The received file is not the expected one; it is removed.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
