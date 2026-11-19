namespace Voicer.UI.Services.Generation;

/// <summary>
/// Turns a text into speech: prepares the sample of the voice, speaks the text piece by piece and joins the pieces.
/// </summary>
public interface IGenerationRunner
{
    /// <summary>
    /// Speaks a text. The result of the previous generation is removed first: its player must be closed.
    /// </summary>
    /// <param name="request">What to speak and how.</param>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="cancellationToken">Stops the generation at once; nothing of it stays.</param>
    /// <returns>The generated speech.</returns>
    /// <exception cref="FileNotFoundException">llama.cpp, FFmpeg, the model or the sample is missing.</exception>
    /// <exception cref="InvalidDataException">The engine or FFmpeg failed.</exception>
    /// <exception cref="ArgumentException">The text has nothing to speak.</exception>
    Task<GenerationResult> RunAsync(GenerationRequest request, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Removes what generations left in the temporary folder.
    /// </summary>
    void ClearLeftovers();
}
