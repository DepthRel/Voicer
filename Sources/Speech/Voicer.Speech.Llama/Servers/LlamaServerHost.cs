namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// Where every llama-server of the application listens, and how it learns its key.
/// </summary>
public static class LlamaServerHost
{
    /// <summary>
    /// The address a server listens on: this computer only, never the network.
    /// </summary>
    public const string ADDRESS = "127.0.0.1";

    /// <summary>
    /// The variable of the environment llama-server reads its key from in place of <c>--api-key</c>: the environment of
    /// a process is open to its own user only, its arguments to every user of the computer.
    /// </summary>
    public const string API_KEY_VARIABLE = "LLAMA_API_KEY";
}
