namespace Voicer.Speech.Llama.Enums;

/// <summary>
/// What the llama-server of the search by meaning is doing.
/// </summary>
public enum EmbeddingServerState
{
    /// <summary>
    /// No server runs; the next search by meaning starts one.
    /// </summary>
    Stopped,

    /// <summary>
    /// The server is loading its model.
    /// </summary>
    Starting,

    /// <summary>
    /// The server runs and waits for a search; it stops by itself after a pause.
    /// </summary>
    Idle,

    /// <summary>
    /// The server is making vectors for a search right now.
    /// </summary>
    Busy,
}
