namespace Voicer.Core.Synthesis;

/// <summary>
/// Speaks a piece of text into a WAV file. Every call loads the model, speaks and gives the memory back: the model does
/// not stay in memory between the calls or after them.
/// </summary>
public interface ISpeechSynthesizer
{
    /// <summary>
    /// Speaks a piece of text.
    /// </summary>
    /// <param name="voice">How the text is spoken.</param>
    /// <param name="text">The text, a few sentences.</param>
    /// <param name="destination">The WAV file to write; it is replaced.</param>
    /// <param name="cancellationToken">Stops the speech at once.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="FileNotFoundException">The engine or a file of the model is missing.</exception>
    /// <exception cref="InvalidDataException">The engine failed.</exception>
    Task SynthesizeAsync(SynthesisVoice voice, string text, string destination, CancellationToken cancellationToken);
}
