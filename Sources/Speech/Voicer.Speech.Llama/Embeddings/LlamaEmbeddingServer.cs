using Voicer.Core.Embeddings;
using Voicer.Speech.Llama.Servers;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// The llama-server of the search by meaning: a model of embeddings, started by a search and ended after a pause
/// without searches.
/// </summary>
public sealed class LlamaEmbeddingServer : LlamaModelServer<EmbeddingModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaEmbeddingServer"/> class.
    /// </summary>
    /// <param name="locator">Finds llama-server.</param>
    /// <param name="store">Where the models are.</param>
    /// <param name="launcher">Starts the process.</param>
    /// <param name="time">The clock of the pause and of the start.</param>
    /// <param name="idleTime">How long a server waits without searches; the default pause when not given.</param>
    /// <param name="startTime">How long a server may take to get ready; the default time when not given.</param>
    public LlamaEmbeddingServer(
        ILlamaCppLocator locator,
        IEmbeddingModelStore store,
        IServerProcessLauncher launcher,
        TimeProvider time,
        TimeSpan? idleTime = null,
        TimeSpan? startTime = null)
        : base(locator, store, launcher, time, idleTime, startTime)
    {
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> Arguments(string modelFile, EmbeddingModel model, string apiKey)
    {
        return LlamaServerCommandLine.Build(modelFile, model, apiKey);
    }
}
