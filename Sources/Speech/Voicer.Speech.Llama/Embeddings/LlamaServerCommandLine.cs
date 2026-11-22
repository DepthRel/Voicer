using System.Globalization;
using Voicer.Core.Embeddings;
using Voicer.Speech.Llama.Servers;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// The arguments of llama-server for a model of embeddings: only the embeddings, only this computer, a free port the
/// system picks, a key that only the application knows, and no web page.
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
    /// <param name="apiKey">The key the server asks every request for.</param>
    /// <returns>The arguments, each on its own.</returns>
    public static IReadOnlyList<string> Build(string modelFile, EmbeddingModel model, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFile);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // A model of embeddings sees a whole text at once: a batch and a slot both take the longest text.
        var slot = model.SlotTokens.ToString(CultureInfo.InvariantCulture);

        return
        [
            "-m", modelFile,
            "--embedding",
            "--alias", model.Id,
            "--host", LlamaServerHost.ADDRESS,
            "--port", "0",
            "--api-key", apiKey,
            "--no-webui",
            "--parallel", SLOTS.ToString(CultureInfo.InvariantCulture),
            "--ctx-size", (model.SlotTokens * SLOTS).ToString(CultureInfo.InvariantCulture),
            "--batch-size", slot,
            "--ubatch-size", slot,
        ];
    }
}
