using Voicer.Media;

namespace Voicer.UI.Services.History;

/// <summary>
/// The folder of the sound files that live only for a while: a sound compressed before it goes into the history, and
/// a sound taken out of the history for the player. Files left by a crash are removed on the next start.
/// </summary>
public sealed class TemporaryAudioFolder
{
    /// <summary>
    /// The files of the folder.
    /// </summary>
    private const string PATTERN = "*" + FfmpegAudioEncoder.EXTENSION;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporaryAudioFolder"/> class.
    /// </summary>
    /// <param name="directory">The folder (<c>Data/Temp/Audio</c>).</param>
    public TemporaryAudioFolder(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory = Path.GetFullPath(directory);
    }

    /// <summary>
    /// Gets the folder.
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Names a new file in the folder; the folder is created when it is missing.
    /// </summary>
    /// <returns>The full path of the file; it does not exist yet.</returns>
    public string NewFile()
    {
        System.IO.Directory.CreateDirectory(Directory);

        return Path.Combine(Directory, Guid.NewGuid().ToString("N") + FfmpegAudioEncoder.EXTENSION);
    }

    /// <summary>
    /// Removes a file of the folder, if it can; one that stays is removed on the next start.
    /// </summary>
    /// <param name="file">The file.</param>
    public void Delete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in use or locked: ClearLeftovers takes it on the next start.
        }
    }

    /// <summary>
    /// Removes the files a previous run left behind.
    /// </summary>
    public void ClearLeftovers()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return;
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(Directory, PATTERN))
        {
            Delete(file);
        }
    }
}
