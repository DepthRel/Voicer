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
    public string ExecutableName => _isWindows ? "llama-tts.exe" : "llama-tts";

    /// <inheritdoc />
    public string? Locate()
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

        return Directory.EnumerateFiles(InstallDirectory, ExecutableName, options).Order(StringComparer.Ordinal).FirstOrDefault();
    }
}
