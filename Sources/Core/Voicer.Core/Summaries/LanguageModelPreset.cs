namespace Voicer.Core.Summaries;

/// <summary>
/// A well-known program that serves language models on this computer, with the address its API answers by default.
/// </summary>
/// <param name="Name">The name of the program; a product name, the same in every language.</param>
/// <param name="Address">The default address of its OpenAI-compatible API.</param>
public sealed record LanguageModelPreset(string Name, string Address)
{
    /// <summary>
    /// Gets the programs Voicer knows, the most common first.
    /// </summary>
    public static IReadOnlyList<LanguageModelPreset> All { get; } =
    [
        new("LM Studio", "http://localhost:1234/v1"),
        new("Ollama", "http://localhost:11434/v1"),
        new("llama.cpp (llama-server)", "http://localhost:8080/v1"),
        new("Jan", "http://localhost:1337/v1"),
    ];
}
