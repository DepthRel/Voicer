namespace Voicer.UI.Services.Lifetime;

/// <summary>
/// Tells the first running window of the application over a data folder from the others: it holds the lock file of
/// the folder for as long as it runs. Only that window clears what earlier runs left in the temporary folder: another
/// one would delete the sound files the first one is working with.
/// </summary>
/// <remarks>
/// The file is opened without sharing: Windows refuses a second opening, Linux and macOS take an advisory lock of the
/// file. The lock ends with the process, however it ends.
/// </remarks>
public sealed class DataFolderLock : IDisposable
{
    /// <summary>
    /// The name of the lock file inside the data folder.
    /// </summary>
    private const string FILE_NAME = "voicer.lock";

    /// <summary>
    /// The open lock file, or <see langword="null"/> when another window holds it.
    /// </summary>
    private readonly FileStream? _file;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataFolderLock"/> class.
    /// </summary>
    /// <param name="file">The open lock file, or <see langword="null"/>.</param>
    private DataFolderLock(FileStream? file)
    {
        _file = file;
    }

    /// <summary>
    /// Gets a value indicating whether this window holds the folder: no other window of the application runs over it.
    /// </summary>
    public bool IsHeld => _file is not null;

    /// <summary>
    /// Takes the lock of a data folder.
    /// </summary>
    /// <param name="dataDirectory">The data folder; it is created when it does not exist.</param>
    /// <returns>The lock; <see cref="IsHeld"/> is <see langword="false"/> when another window holds it.</returns>
    public static DataFolderLock Acquire(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        try
        {
            Directory.CreateDirectory(dataDirectory);

            return new DataFolderLock(new FileStream(Path.Combine(dataDirectory, FILE_NAME), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DataFolderLock(null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _file?.Dispose();
    }
}
