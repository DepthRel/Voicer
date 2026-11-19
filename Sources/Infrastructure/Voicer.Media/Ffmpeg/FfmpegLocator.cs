namespace Voicer.Media.Ffmpeg;

/// <summary>
/// <see cref="IFfmpegLocator"/> that looks in the tools folder of the application, the folders of <c>PATH</c> and the
/// usual places of the package managers (an application started from the macOS Finder does not get the
/// <c>PATH</c> of the shell).
/// </summary>
public sealed class FfmpegLocator : IFfmpegLocator
{
    /// <summary>
    /// The name of the folder of FFmpeg in the tools folder.
    /// </summary>
    public const string FOLDER = "FFmpeg";

    /// <summary>
    /// The places where the package managers of Linux and macOS put programs.
    /// </summary>
    private static readonly string[] _unixPlaces = ["/opt/homebrew/bin", "/usr/local/bin", "/usr/bin", "/snap/bin", "/opt/local/bin"];

    /// <summary>
    /// Reads an environment variable.
    /// </summary>
    private readonly Func<string, string?> _environment;

    /// <summary>
    /// Tells whether a file exists.
    /// </summary>
    private readonly Func<string, bool> _fileExists;

    /// <summary>
    /// Whether the system is Windows.
    /// </summary>
    private readonly bool _isWindows;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegLocator"/> class.
    /// </summary>
    /// <param name="toolsDirectory">The tools folder of the application (<c>Data/Tools</c>).</param>
    /// <param name="isWindows">Whether the system is Windows; the system it runs on by default.</param>
    /// <param name="environment">Reads an environment variable; the environment of the process by default.</param>
    /// <param name="fileExists">Tells whether a file exists; the file system by default.</param>
    public FfmpegLocator(
        string toolsDirectory,
        bool? isWindows = null,
        Func<string, string?>? environment = null,
        Func<string, bool>? fileExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);
        InstallDirectory = Path.Combine(Path.GetFullPath(toolsDirectory), FOLDER);
        _isWindows = isWindows ?? OperatingSystem.IsWindows();
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _fileExists = fileExists ?? File.Exists;
    }

    /// <inheritdoc />
    public string InstallDirectory { get; }

    /// <summary>
    /// Gets the file name of the program on this system.
    /// </summary>
    public string ExecutableName => _isWindows ? "ffmpeg.exe" : "ffmpeg";

    /// <inheritdoc />
    public string? Locate()
    {
        return Candidates().FirstOrDefault(_fileExists);
    }

    /// <summary>
    /// Lists the places FFmpeg is looked for, in order.
    /// </summary>
    /// <returns>The full paths of the candidates.</returns>
    private IEnumerable<string> Candidates()
    {
        yield return Path.Combine(InstallDirectory, ExecutableName);

        var separator = _isWindows ? ';' : ':';
        var path = _environment("PATH") ?? string.Empty;
        foreach (var folder in path.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A relative folder of PATH depends on the current folder of the moment: it is not trusted.
            if (Path.IsPathFullyQualified(folder))
            {
                yield return Path.Combine(folder, ExecutableName);
            }
        }

        if (!_isWindows)
        {
            foreach (var folder in _unixPlaces)
            {
                yield return Path.Combine(folder, ExecutableName);
            }
        }
    }
}
