namespace Voicer.Core.Search;

/// <summary>
/// Turns texts into vectors for the search by meaning: a model on a server of language models, or a model of the
/// application run by its own llama.cpp.
/// </summary>
public interface ITextEmbedder
{
    /// <summary>
    /// Gets the name the vectors of the model are kept under: vectors of different models never mix.
    /// </summary>
    string Model { get; }

    /// <summary>
    /// Makes a vector of every text.
    /// </summary>
    /// <param name="texts">The texts, with the prefixes of the model already added (<see cref="EmbeddingText"/>).</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A vector per text, in the order of the texts.</returns>
    /// <exception cref="HttpRequestException">The model cannot be reached or refused the texts.</exception>
    /// <exception cref="InvalidDataException">The answer cannot be read, or the model could not be started.</exception>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
