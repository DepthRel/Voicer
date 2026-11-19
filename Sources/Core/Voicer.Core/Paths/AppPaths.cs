namespace Voicer.Core.Paths;

/// <summary>
/// The folders of the data of the application: downloaded models and tools, settings, temporary files. Everything lives
/// in one data folder, <c>Data</c>, inside the folder of the application.
/// </summary>
/// <remarks>
/// <para>
/// A package (the archive, the installation for the current user) marks its root with <see cref="PORTABLE_MARKER"/>: the
/// program sits in a subfolder, and the data folder goes next to the marker, at the root. Without the marker the data
/// folder sits next to the program itself.
/// </para>
/// <para>
/// A folder the user cannot write to (a system-wide installation) cannot hold the data: then it goes to the profile of
/// the user. <c>--data &lt;folder&gt;</c> on the command line names the data folder explicitly.
/// </para>
/// </remarks>
public sealed class AppPaths
{
    /// <summary>
    /// The file that marks the root of a package: the data folder is created next to it.
    /// </summary>
    public const string PORTABLE_MARKER = "voicer.portable";

    /// <summary>
    /// The name of the data folder.
    /// </summary>
    public const string DATA_FOLDER = "Data";

    /// <summary>
    /// The command line option that names the data folder.
    /// </summary>
    public const string DATA_OPTION = "--data";

    /// <summary>
    /// The name of the folder of the application in the profile of the user.
    /// </summary>
    private const string PROFILE_FOLDER = "Voicer";

    /// <summary>
    /// Initializes a new instance of the <see cref="AppPaths"/> class.
    /// </summary>
    /// <param name="dataDirectory">The data folder.</param>
    public AppPaths(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        DataDirectory = Path.GetFullPath(dataDirectory);
    }

    /// <summary>
    /// Gets the data folder.
    /// </summary>
    public string DataDirectory { get; }

    /// <summary>
    /// Gets the folder of the downloaded speech models, a folder per model.
    /// </summary>
    public string ModelsDirectory => Path.Combine(DataDirectory, "Models");

    /// <summary>
    /// Gets the folder of the downloaded tools, a folder per tool.
    /// </summary>
    public string ToolsDirectory => Path.Combine(DataDirectory, "Tools");

    /// <summary>
    /// Gets the folder of the temporary files of a recognition; it is emptied when the application starts.
    /// </summary>
    public string TempDirectory => Path.Combine(DataDirectory, "Temp");

    /// <summary>
    /// Gets the file of the settings of the user.
    /// </summary>
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    /// <summary>
    /// Gets the database of the history: the recognized texts and what belongs to them.
    /// </summary>
    public string DatabaseFile => Path.Combine(DataDirectory, "voicer.db");

    /// <summary>
    /// Chooses the data folder.
    /// </summary>
    /// <param name="baseDirectory">The folder of the program.</param>
    /// <param name="args">The command line arguments; <see cref="DATA_OPTION"/> names the data folder.</param>
    /// <param name="isWritable">Tells whether the user can write into a folder (it may create the folder).</param>
    /// <param name="profileDirectory">
    /// The folder of the local data of the user (<see cref="Environment.SpecialFolder.LocalApplicationData"/>).
    /// </param>
    /// <returns>The paths of the data.</returns>
    public static AppPaths Resolve(
        string baseDirectory,
        IReadOnlyList<string> args,
        Func<string, bool> isWritable,
        string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(isWritable);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDirectory);

        var explicitDirectory = ReadOption(args, DATA_OPTION);
        if (explicitDirectory is not null)
        {
            return new AppPaths(explicitDirectory);
        }

        var root = FindPackageRoot(Path.GetFullPath(baseDirectory)) ?? Path.GetFullPath(baseDirectory);
        var local = Path.Combine(root, DATA_FOLDER);
        if (isWritable(local))
        {
            return new AppPaths(local);
        }

        return new AppPaths(Path.Combine(profileDirectory, PROFILE_FOLDER, DATA_FOLDER));
    }

    /// <summary>
    /// Tells whether the user can write into a folder, creating it when it is missing.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <returns><see langword="true"/> when a file could be created and removed there.</returns>
    public static bool CanWrite(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the value of a command line option given as <c>--name value</c> or <c>--name=value</c>.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <param name="name">The name of the option.</param>
    /// <returns>The value, or <see langword="null"/> when the option is absent or empty.</returns>
    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal) && i + 1 < args.Count)
            {
                return string.IsNullOrWhiteSpace(args[i + 1]) ? null : args[i + 1];
            }

            if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
            {
                var value = args[i][(name.Length + 1)..];

                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the root of a package: the folder of the program or the folder above it, whichever holds
    /// <see cref="PORTABLE_MARKER"/>.
    /// </summary>
    /// <param name="baseDirectory">The full path of the folder of the program.</param>
    /// <returns>The root, or <see langword="null"/> when the program is not in a package.</returns>
    private static string? FindPackageRoot(string baseDirectory)
    {
        var folder = new DirectoryInfo(baseDirectory);
        foreach (var candidate in new[] { folder, folder.Parent })
        {
            if (candidate is not null && File.Exists(Path.Combine(candidate.FullName, PORTABLE_MARKER)))
            {
                return candidate.FullName;
            }
        }

        return null;
    }
}
