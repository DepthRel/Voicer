namespace Voicer.Speech.Llama.Enums;

/// <summary>
/// What a llama-server of the application is doing.
/// </summary>
public enum LlamaServerState
{
    /// <summary>
    /// No server runs; the next request starts one.
    /// </summary>
    Stopped,

    /// <summary>
    /// The server is loading its model.
    /// </summary>
    Starting,

    /// <summary>
    /// The server runs and waits for a request; it stops by itself after a pause.
    /// </summary>
    Idle,

    /// <summary>
    /// The server is answering a request right now.
    /// </summary>
    Busy,
}
