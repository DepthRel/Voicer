namespace Voicer.UI.Services.Summaries;

/// <summary>
/// The model that writes the summaries and the answers to questions, as the user chose it: a model of the server of
/// language models, or a model of Voicer in its own llama-server.
/// </summary>
public interface IChatModelSource
{
    /// <summary>
    /// Gets a value indicating whether a model is chosen and can be used: its server set up, or its file and llama.cpp
    /// installed.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Makes the chosen model ready for a piece of work, starting the llama-server of a model of Voicer; the session
    /// holds that server until it is disposed.
    /// </summary>
    /// <param name="cancellationToken">Stops the waiting for the start.</param>
    /// <returns>The session, with the settings the requests use.</returns>
    /// <exception cref="InvalidOperationException">No model is chosen or it cannot be used.</exception>
    /// <exception cref="FileNotFoundException">llama.cpp or the model of Voicer is not installed.</exception>
    /// <exception cref="InvalidDataException">The llama-server could not be started.</exception>
    Task<ChatSession> OpenAsync(CancellationToken cancellationToken);
}
