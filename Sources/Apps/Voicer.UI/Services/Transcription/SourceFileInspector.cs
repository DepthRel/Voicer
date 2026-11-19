using Voicer.Localization;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// <see cref="ISourceFileInspector"/> over the file system: the file is opened for reading once, on a thread of the
/// pool, so a file that is locked, missing or on a disk that went away is refused with a clear message.
/// </summary>
public sealed class SourceFileInspector : ISourceFileInspector
{
    /// <inheritdoc />
    public Task<SourceFile> InspectAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => Inspect(path), cancellationToken);
    }

    /// <summary>
    /// Checks a file on the calling thread.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The checked file.</returns>
    private static SourceFile Inspect(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw LocalizedError.Create(m => new IOException(m, ex), "Transcription.SourceInvalidPath");
        }

        if (!File.Exists(fullPath))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m, fullPath), "Transcription.SourceMissing");
        }

        // Opening proves the file can be read now: not locked, not on a disk that is gone.
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length == 0)
        {
            throw LocalizedError.Create(m => new IOException(m), "Transcription.SourceEmpty");
        }

        return new SourceFile(fullPath, stream.Length);
    }
}
