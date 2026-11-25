using System.Globalization;
using Voicer.Core.Embeddings;
using Voicer.Speech.Llama.Servers;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// The arguments of llama-server for a model of embeddings: only the embeddings, only this computer, a free port the
/// system picks, and no web page. The key that only the application knows goes in the environment of the process
/// (<see cref="IServerProcessLauncher"/>), not here, where other users of the computer could read it.
/// </summary>
public static class LlamaServerCommandLine
{
    /// <summary>
    /// How many texts the server embeds at once, each in a slot of its own.
    /// </summary>
    private const int SLOTS = 4;

    /// <summary>
    /// Builds the arguments.
    /// </summary>
    /// <param name="modelFile">The full path of the file of the model.</param>
    /// <param name="model">The model: its identifier names it in the requests, its slot sizes the context.</param>
    /// <returns>The arguments, each on its own.</returns>
    public static IReadOnlyList<string> Build(string modelFile, EmbeddingModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFile);
        ArgumentNullException.ThrowIfNull(model);

        // A model of embeddings sees a whole text at once: a batch and a slot both take the longest text.
        var slot = model.SlotTokens.ToString(CultureInfo.InvariantCulture);

        return
        [
            "-m", modelFile,
            "--embedding",
            "--alias", model.Id,
            "--host", LlamaServerHost.ADDRESS,
            "--port", "0",
            "--no-webui",
            "--parallel", SLOTS.ToString(CultureInfo.InvariantCulture),
            "--ctx-size", (model.SlotTokens * SLOTS).ToString(CultureInfo.InvariantCulture),
            "--batch-size", slot,
            "--ubatch-size", slot,
        ];
    }
}
