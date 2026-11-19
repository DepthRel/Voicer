using System.Globalization;
using System.Text;

namespace Voicer.UI.Services.Diagnostics;

/// <summary>
/// <see cref="IErrorLog"/> in a text file of the data folder (<c>Data/errors.log</c>): a failure per entry, with the
/// moment, the type, the message and the stack; a file grown past its limit starts again.
/// </summary>
public sealed class ErrorLog : IErrorLog
{
    /// <summary>
    /// The size past which the log starts again, in bytes.
    /// </summary>
    private const long MAX_BYTES = 1024 * 1024;

    /// <summary>
    /// The clock that dates the entries.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Lets one failure be written at a time.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorLog"/> class.
    /// </summary>
    /// <param name="filePath">The file of the log; it and its folder are created when they are missing.</param>
    /// <param name="time">The clock that dates the entries; the system clock by default.</param>
    public ErrorLog(string filePath, TimeProvider? time = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = Path.GetFullPath(filePath);
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public bool Write(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var entry = new StringBuilder()
            .Append(_time.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture))
            .Append('\n')
            .Append(failure)
            .Append("\n\n")
            .ToString();

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var existing = new FileInfo(FilePath);
                if (existing.Exists && existing.Length > MAX_BYTES)
                {
                    existing.Delete();
                }

                File.AppendAllText(FilePath, entry, Encoding.UTF8);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
