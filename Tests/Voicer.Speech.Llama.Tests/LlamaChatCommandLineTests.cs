using FluentAssertions;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Chat;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the arguments of the llama-server that writes summaries and answers with a model of Voicer.
/// </summary>
public sealed class LlamaChatCommandLineTests
{
    /// <summary>
    /// The server listens on this computer only, on a free port, one request at a time in the context of
    /// the model, and never makes vectors; its key is not among the arguments, which every user can read.
    /// </summary>
    [Fact]
    public void Build_ServesTheModelOnThisComputerOnly()
    {
        var model = Model(thinks: false);

        var arguments = LlamaChatCommandLine.Build("model.gguf", model);

        arguments.Should().ContainInConsecutiveOrder("-m", "model.gguf");
        arguments.Should().ContainInConsecutiveOrder("--alias", "tiny-chat");
        arguments.Should().ContainInConsecutiveOrder("--host", "127.0.0.1");
        arguments.Should().ContainInConsecutiveOrder("--port", "0");
        arguments.Should().NotContain("--api-key");
        arguments.Should().ContainInConsecutiveOrder("--parallel", "1");
        arguments.Should().ContainInConsecutiveOrder("--ctx-size", "4096");
        arguments.Should().Contain("--no-webui");
        arguments.Should().NotContain("--embedding");
        arguments.Should().NotContain("--reasoning-budget");
    }

    /// <summary>
    /// A model that reasons aloud is told not to, so a summary is not spent on its reasoning.
    /// </summary>
    [Fact]
    public void Build_TellsAThinkingModelNotToReason()
    {
        var arguments = LlamaChatCommandLine.Build("model.gguf", Model(thinks: true));

        arguments.Should().ContainInConsecutiveOrder("--reasoning-budget", "0");
    }

    /// <summary>
    /// Creates a model of the tests.
    /// </summary>
    /// <param name="thinks">Whether it reasons aloud.</param>
    /// <returns>The model.</returns>
    private static ChatModel Model(bool thinks)
    {
        return new ChatModel("tiny-chat", "Tiny Chat", "tiny.gguf", new Uri("https://example.org/tiny.gguf"), 10, new string('a', 64), 4096, thinks);
    }
}
