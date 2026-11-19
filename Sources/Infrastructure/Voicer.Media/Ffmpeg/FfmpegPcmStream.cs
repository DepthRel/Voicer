using System.Diagnostics;
using Voicer.Localization;

namespace Voicer.Media.Ffmpeg;

/// <summary>
/// The samples FFmpeg writes to its standard output, as a read-only stream: a failure of FFmpeg is thrown when the
/// stream ends, and disposing the stream ends FFmpeg.
/// </summary>
internal sealed class FfmpegPcmStream : Stream
{
    /// <summary>
    /// The process of FFmpeg.
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// The standard output of FFmpeg.
    /// </summary>
    private readonly Stream _samples;

    /// <summary>
    /// Reads the log of FFmpeg, so it never blocks on a full pipe.
    /// </summary>
    private readonly Task _logReader;

    /// <summary>
    /// The last line of the log of FFmpeg, which explains a failure.
    /// </summary>
    private string _lastLogLine = string.Empty;

    /// <summary>
    /// Whether the stream is disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegPcmStream"/> class.
    /// </summary>
    /// <param name="process">The started process of FFmpeg, with its standard output and error redirected.</param>
    internal FfmpegPcmStream(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _samples = process.StandardOutput.BaseStream;
        _logReader = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                _lastLogLine = line;
            }
        });
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var read = await _samples.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            return read;
        }

        // The end of the samples: FFmpeg has finished, well or not.
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await _logReader.ConfigureAwait(false);
        if (_process.ExitCode != 0)
        {
            throw LocalizedError.Create(m => new IOException(m), "Player.DecodeFailed", _process.ExitCode, _lastLogLine);
        }

        return 0;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            FfmpegConversion.Stop(_process);
            _process.Dispose();
        }

        base.Dispose(disposing);
    }
}
