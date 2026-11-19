using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Voicer.Downloads.Enums;
using Voicer.Localization;

namespace Voicer.Downloads;

/// <summary>
/// <see cref="IFileDownloader"/> over HTTPS with <see cref="HttpClient"/>.
/// </summary>
public sealed class FileDownloader : IFileDownloader
{
    /// <summary>
    /// The extension of the part of a file that is being received.
    /// </summary>
    public const string PART_EXTENSION = ".part";

    /// <summary>
    /// The size of the buffer the file is copied and hashed with.
    /// </summary>
    private const int BUFFER_SIZE = 1024 * 1024;

    /// <summary>
    /// How often the progress is reported.
    /// </summary>
    private static readonly TimeSpan _reportInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The client that receives the files.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// The clock of the speed measurement.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileDownloader"/> class.
    /// </summary>
    /// <param name="http">The client that receives the files; its timeout must allow downloads of several gigabytes.</param>
    /// <param name="time">The clock of the speed measurement; the system clock by default.</param>
    public FileDownloader(HttpClient http, TimeProvider? time = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Source.Scheme != Uri.UriSchemeHttps)
        {
            throw LocalizedError.Argument(nameof(request), "Download.NotHttps", request.Source);
        }

        var destination = Path.GetFullPath(request.Destination);
        var part = destination + PART_EXTENSION;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // A part longer than the file cannot be its beginning: it is received again.
        var received = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (received > request.SizeBytes)
        {
            File.Delete(part);
            received = 0;
        }

        if (received < request.SizeBytes)
        {
            await ReceiveAsync(request, part, received, progress, cancellationToken).ConfigureAwait(false);
        }

        await VerifyAsync(request, part, progress, cancellationToken).ConfigureAwait(false);

        File.Move(part, destination, overwrite: true);
    }

    /// <summary>
    /// Receives the rest of a file into its part, asking the server to continue from the received length.
    /// </summary>
    /// <param name="request">The file.</param>
    /// <param name="part">The path of the part.</param>
    /// <param name="received">How many bytes the part already holds.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the part holds the whole length.</returns>
    private async Task ReceiveAsync(
        DownloadRequest request,
        string part,
        long received,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Source);
        if (received > 0)
        {
            message.Headers.Range = new RangeHeaderValue(received, null);
        }

        using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw LocalizedError.Create(m => new HttpRequestException(m, null, response.StatusCode), "Download.HttpFailed", (int)response.StatusCode);
        }

        // A server that ignores the range sends the whole file: the part starts over.
        var resumed = received > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed)
        {
            received = 0;
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE, useAsync: true);

        var meter = new SpeedMeter(_time, received);
        var buffer = new byte[BUFFER_SIZE];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            received += read;
            if (received > request.SizeBytes)
            {
                throw LocalizedError.Create(m => new InvalidDataException(m), "Download.TooLarge", request.Source);
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (meter.TryMeasure(received, out var speed))
            {
                progress?.Report(new DownloadProgress(DownloadStage.Downloading, received, request.SizeBytes, speed));
            }
        }

        progress?.Report(new DownloadProgress(DownloadStage.Downloading, received, request.SizeBytes, 0));
    }

    /// <summary>
    /// Checks the size and the SHA-256 of a received part; a part that is not the expected file is removed.
    /// </summary>
    /// <param name="request">The file.</param>
    /// <param name="part">The path of the part.</param>
    /// <param name="progress">Receives the progress, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>A task that completes when the part is checked.</returns>
    private async Task VerifyAsync(DownloadRequest request, string part, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        var length = new FileInfo(part).Length;
        if (length != request.SizeBytes)
        {
            File.Delete(part);

            throw LocalizedError.Create(m => new InvalidDataException(m), "Download.SizeMismatch", length, request.SizeBytes);
        }

        using var sha = SHA256.Create();
        await using (var stream = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, useAsync: true))
        {
            var meter = new SpeedMeter(_time, 0);
            var buffer = new byte[BUFFER_SIZE];
            long hashed = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
                hashed += read;
                if (meter.TryMeasure(hashed, out var speed))
                {
                    progress?.Report(new DownloadProgress(DownloadStage.Verifying, hashed, length, speed));
                }
            }
        }

        sha.TransformFinalBlock([], 0, 0);
        var actual = Convert.ToHexStringLower(sha.Hash!);
        if (!string.Equals(actual, request.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(part);

            throw LocalizedError.Create(m => new InvalidDataException(m), "Download.HashMismatch", request.Source);
        }
    }

    /// <summary>
    /// Measures the speed of a stage between two reports.
    /// </summary>
    private sealed class SpeedMeter
    {
        /// <summary>
        /// The clock.
        /// </summary>
        private readonly TimeProvider _time;

        /// <summary>
        /// The timestamp of the last report.
        /// </summary>
        private long _lastTimestamp;

        /// <summary>
        /// The number of bytes at the last report.
        /// </summary>
        private long _lastBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedMeter"/> class.
        /// </summary>
        /// <param name="time">The clock.</param>
        /// <param name="startBytes">The number of bytes the stage starts with.</param>
        public SpeedMeter(TimeProvider time, long startBytes)
        {
            _time = time;
            _lastTimestamp = time.GetTimestamp();
            _lastBytes = startBytes;
        }

        /// <summary>
        /// Measures the speed when the report interval has passed since the last report.
        /// </summary>
        /// <param name="bytes">The number of bytes done now.</param>
        /// <param name="bytesPerSecond">The speed since the last report.</param>
        /// <returns><see langword="true"/> when it is time to report.</returns>
        public bool TryMeasure(long bytes, out double bytesPerSecond)
        {
            var now = _time.GetTimestamp();
            var elapsed = _time.GetElapsedTime(_lastTimestamp, now);
            if (elapsed < _reportInterval)
            {
                bytesPerSecond = 0;

                return false;
            }

            bytesPerSecond = (bytes - _lastBytes) / elapsed.TotalSeconds;
            _lastTimestamp = now;
            _lastBytes = bytes;

            return true;
        }
    }
}
