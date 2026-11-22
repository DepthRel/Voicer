using System.ComponentModel;
using System.Diagnostics;
using Voicer.Localization;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// <see cref="IServerProcessLauncher"/> that starts the program directly, without a shell and without a window, every
/// argument on its own, in the folder of the program so it finds its libraries.
/// </summary>
public sealed class ServerProcessLauncher : IServerProcessLauncher
{
    /// <inheritdoc />
    public IServerProcess Start(string program, IReadOnlyList<string> arguments, Func<string, bool> isReady)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(program);
        ArgumentNullException.ThrowIfNull(arguments);
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

        return new ServerProcess(process, isReady);
    }
}
