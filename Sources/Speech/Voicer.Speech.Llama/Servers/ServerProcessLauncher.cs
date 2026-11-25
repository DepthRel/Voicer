using System.ComponentModel;
using System.Diagnostics;
using Voicer.Localization;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// <see cref="IServerProcessLauncher"/> that starts the program directly, without a shell and without a window, every
/// argument on its own, in the folder of the program so it finds its libraries. The key goes in the environment of the
/// process; the process is put into the job of the application on Windows and noted in the registry of the servers, so
/// it does not outlive the application.
/// </summary>
public sealed class ServerProcessLauncher : IServerProcessLauncher
{
    /// <summary>
    /// The registry of the running servers, or <see langword="null"/> to keep no notes.
    /// </summary>
    private readonly ServerProcessRegistry? _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerProcessLauncher"/> class.
    /// </summary>
    /// <param name="registry">The registry of the running servers, or <see langword="null"/> to keep no notes.</param>
    public ServerProcessLauncher(ServerProcessRegistry? registry = null)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public IServerProcess Start(string program, IReadOnlyList<string> arguments, string apiKey, Func<string, bool> isReady)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(isReady);

        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = Path.GetDirectoryName(program)!,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment[LlamaServerHost.API_KEY_VARIABLE] = apiKey;

        var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw LocalizedError.Create(m => new InvalidDataException(m, ex), "Search.ServerStartFailed", ex.Message);
        }

        ChildProcessJob.Attach(process);
        _registry?.Add(process);
        var id = process.Id;

        return new ServerProcess(process, isReady, _registry is null ? null : () => _registry.Remove(id));
    }
}
