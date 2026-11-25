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
    /// <param name="apiKey">
    /// The key the server asks of every request; it is given in the environment of the process
    /// (<see cref="LlamaServerHost.API_KEY_VARIABLE"/>), never among the arguments.
    /// </param>
    /// <param name="isReady">Tells whether a line of its output says it is ready.</param>
    /// <returns>The running process.</returns>
    /// <exception cref="InvalidDataException">The program could not be started.</exception>
    IServerProcess Start(string program, IReadOnlyList<string> arguments, string apiKey, Func<string, bool> isReady);
}
