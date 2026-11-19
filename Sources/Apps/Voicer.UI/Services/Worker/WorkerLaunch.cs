using System.Reflection;
using Voicer.Core.Protocol;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// How the worker process is started: the program of this application again, with
/// <see cref="WorkerProtocol.WORKER_SWITCH"/>.
/// </summary>
/// <param name="FileName">The program to start.</param>
/// <param name="Prefix">The arguments that come before <see cref="WorkerProtocol.WORKER_SWITCH"/>.</param>
public sealed record WorkerLaunch(string FileName, IReadOnlyList<string> Prefix)
{
    /// <summary>
    /// Describes the start of a worker of the running application: its own executable, or the <c>dotnet</c> host with the
    /// assembly when the application was started that way.
    /// </summary>
    /// <returns>The launch.</returns>
    /// <exception cref="InvalidOperationException">The path of the running program is unknown.</exception>
    public static WorkerLaunch ForCurrentProcess()
    {
        var process = Environment.ProcessPath ??
            throw new InvalidOperationException("The path of the running program is unknown.");

        var host = Path.GetFileNameWithoutExtension(process);
        if (string.Equals(host, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(assembly))
            {
                throw new InvalidOperationException("The assembly of the running program is unknown.");
            }

            return new WorkerLaunch(process, [assembly]);
        }

        return new WorkerLaunch(process, []);
    }

    /// <summary>
    /// Gets every argument of a worker command.
    /// </summary>
    /// <param name="command">The command and its options.</param>
    /// <returns>The arguments of the process.</returns>
    public IReadOnlyList<string> ArgumentsFor(IReadOnlyList<string> command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var parent = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return [.. Prefix, WorkerProtocol.WORKER_SWITCH, WorkerProtocol.PARENT_OPTION, parent, .. command];
    }
}
