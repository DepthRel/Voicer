namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// <see cref="ILlamaCppLocator"/> over the tools folder of the data folder.
/// </summary>
public sealed class LlamaCppLocator : ILlamaCppLocator
{
    /// <summary>
    /// The name of the folder of llama.cpp in the tools folder.
    /// </summary>
    public const string FOLDER = "LlamaCpp";

    /// <summary>
    /// How deep the program is looked for: the archive of Linux and macOS keeps the files in a folder of its own.
    /// </summary>
    private const int MAX_DEPTH = 2;

    /// <summary>
    /// Whether the system is Windows.
    /// </summary>
    private readonly bool _isWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaCppLocator"/> class.
    /// </summary>
    /// <param name="toolsDirectory">The tools folder of the application (<c>Data/Tools</c>).</param>
    /// <param name="isWindows">Whether the system is Windows; the system it runs on by default.</param>
    public LlamaCppLocator(string toolsDirectory, bool? isWindows = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);
        RootDirectory = Path.Combine(Path.GetFullPath(toolsDirectory), FOLDER);
        InstallDirectory = Path.Combine(RootDirectory, LlamaCppPackage.VERSION);
        _isWindows = isWindows ?? OperatingSystem.IsWindows();
    }

    /// <inheritdoc />
    public string RootDirectory { get; }

    /// <inheritdoc />
    public string InstallDirectory { get; }

    /// <summary>
    /// Gets the file name of the program on this system.
    /// </summary>
    public string ExecutableName => ProgramName("llama-tts");

    /// <summary>
    /// Gets the file name of the server of the build on this system.
    /// </summary>
    public string ServerName => ProgramName("llama-server");

    /// <inheritdoc />
    public string? Locate()
    {
        return Find(ExecutableName);
    }

    /// <inheritdoc />
    public string? LocateServer()
    {
        return Find(ServerName);
    }

    /// <summary>
    /// Gets the file name of a program of the build on this system.
    /// </summary>
    /// <param name="name">The name of the program without an extension.</param>
    /// <returns>The file name.</returns>
    private string ProgramName(string name)
    {
        return _isWindows ? name + ".exe" : name;
    }

    /// <summary>
    /// Finds a program in the folder of the pinned build, a level or two deep, the way its archive lays it out.
    /// </summary>
    /// <param name="fileName">The file name of the program.</param>
    /// <returns>The full path of the program, or <see langword="null"/> when it is not there.</returns>
    private string? Find(string fileName)
    {
        if (!Directory.Exists(InstallDirectory))
        {
            return null;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MAX_DEPTH,
            MatchCasing = MatchCasing.CaseSensitive,
            IgnoreInaccessible = true,
        };

        return Directory.EnumerateFiles(InstallDirectory, fileName, options).Order(StringComparer.Ordinal).FirstOrDefault();
    }
}
