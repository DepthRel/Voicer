using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Servers;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Chat;

/// <summary>
/// The llama-server of a model of Voicer that writes summaries and answers: started by a summary or a question and
/// ended after a pause without them.
/// </summary>
public sealed class LlamaChatServer : LlamaModelServer<ChatModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaChatServer"/> class.
    /// </summary>
    /// <param name="locator">Finds llama-server.</param>
    /// <param name="store">Where the models are.</param>
    /// <param name="launcher">Starts the process.</param>
    /// <param name="time">The clock of the pause and of the start.</param>
    public LlamaChatServer(ILlamaCppLocator locator, IChatModelStore store, IServerProcessLauncher launcher, TimeProvider time)
        : base(locator, store, launcher, time)
    {
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> Arguments(string modelFile, ChatModel model, string apiKey)
    {
        return LlamaChatCommandLine.Build(modelFile, model, apiKey);
    }
}
