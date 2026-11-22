namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// Shows a llama-server of the application to the user and lets them end it.
/// </summary>
public interface ILlamaServerMonitor
{
    /// <summary>
    /// Reads the state of the server and what its process takes now.
    /// </summary>
    /// <returns>The state.</returns>
    LlamaServerStatus Status();

    /// <summary>
    /// Ends the server after a pause, unless it is starting or a request uses it.
    /// </summary>
    /// <returns><see langword="true"/> when no server runs any more; <see langword="false"/> when it is in use.</returns>
    bool TryStop();

    /// <summary>
    /// Ends the server at once, giving its memory back, even while it starts or a request uses it: that request fails,
    /// and the next one starts the server again.
    /// </summary>
    void Stop();
}
