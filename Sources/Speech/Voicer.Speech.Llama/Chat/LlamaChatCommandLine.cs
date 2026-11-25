using System.Globalization;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Servers;

namespace Voicer.Speech.Llama.Chat;

/// <summary>
/// Builds the arguments of the llama-server that writes summaries and answers with a model of Voicer.
/// </summary>
public static class LlamaChatCommandLine
{
    /// <summary>
    /// How many requests the server answers at once: the summaries and the answers ask one after another.
    /// </summary>
    private const int SLOTS = 1;

    /// <summary>
    /// Builds the arguments for a model: this computer only, a free port, the context of the model. The key goes in the
    /// environment of the process (<see cref="IServerProcessLauncher"/>), not here, where other users could read it.
    /// </summary>
    /// <param name="modelFile">The file of the weights of the model.</param>
    /// <param name="model">The model.</param>
    /// <returns>The arguments, without a shell.</returns>
    public static IReadOnlyList<string> Build(string modelFile, ChatModel model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelFile);
        ArgumentNullException.ThrowIfNull(model);

        List<string> arguments =
        [
            "-m", modelFile,
            "--alias", model.Id,
            "--host", LlamaServerHost.ADDRESS,
            "--port", "0",
            "--no-webui",
            "--parallel", SLOTS.ToString(CultureInfo.InvariantCulture),
            "--ctx-size", model.ContextTokens.ToString(CultureInfo.InvariantCulture),
        ];

        // A model that reasons aloud would spend the context of a summary on its reasoning.
        if (model.Thinks)
        {
            arguments.AddRange(["--reasoning-budget", "0"]);
        }

        return arguments;
    }
}
