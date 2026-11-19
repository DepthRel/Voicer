namespace Voicer.UI.Services.Transcription;

/// <summary>
/// Gives a recognition the file of the detector of speech, downloading it first when it is not there yet (a model
/// downloaded before the detector came with the models).
/// </summary>
public interface ISpeechDetectorSource
{
    /// <summary>
    /// Gets a value indicating whether the detector is on this computer already.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Makes the detector ready.
    /// </summary>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>The file of the detector.</returns>
    /// <exception cref="HttpRequestException">The detector could not be downloaded.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="InvalidDataException">The downloaded file is not the pinned one.</exception>
    Task<string> PrepareAsync(CancellationToken cancellationToken);
}
