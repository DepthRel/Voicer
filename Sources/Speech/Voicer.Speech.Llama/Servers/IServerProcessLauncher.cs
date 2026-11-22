namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// Starts llama-server as a process of its own, without a shell.
/// </summary>
public interface IServerProcessLauncher
{
    /// <summary>
    /// Starts the program.
    /// </summary>
    /// <param name="program">The full path of the program.</param>
    /// <param name="arguments">Its arguments, each on its own.</param>
    /// <param name="isReady">Tells whether a line of its output says it is ready.</param>
    /// <returns>The running process.</returns>
    /// <exception cref="InvalidDataException">The program could not be started.</exception>
    IServerProcess Start(string program, IReadOnlyList<string> arguments, Func<string, bool> isReady);
}
