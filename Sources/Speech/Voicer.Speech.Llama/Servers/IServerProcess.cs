namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// A running llama-server: whether it is ready, whether it ended and what it said last. Disposing it ends the process.
/// </summary>
public interface IServerProcess : IDisposable
{
    /// <summary>
    /// Gets a task that completes with the first line of the output that tells the server is ready, or with
    /// <see langword="null"/> when the process ended before it.
    /// </summary>
    Task<string?> Ready { get; }

    /// <summary>
    /// Gets a value indicating whether the process has ended.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Gets the line of the output that best explains a failure: the last one that speaks of an error, or the last one.
    /// </summary>
    string LastWords { get; }

    /// <summary>
    /// Reads what the process takes from the computer now.
    /// </summary>
    /// <returns>The usage, or <see langword="null"/> when the process has ended.</returns>
    ServerUsage? ReadUsage();
}
