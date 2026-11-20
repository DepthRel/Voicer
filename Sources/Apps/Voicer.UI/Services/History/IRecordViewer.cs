using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.History;

/// <summary>
/// Opens the window of a record of the history.
/// </summary>
public interface IRecordViewer
{
    /// <summary>
    /// Reads a record from the history and shows it.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <param name="focus">
    /// The found passage the record is opened at: the player starts there and the text shows it marked; or
    /// <see langword="null"/> for the beginning.
    /// </param>
    /// <returns>
    /// A task that completes when the window is closed: <see langword="true"/>, or <see langword="false"/> when the
    /// record is no longer in the history.
    /// </returns>
    /// <exception cref="IOException">The history could not be read.</exception>
    Task<bool> OpenAsync(long id, CancellationToken cancellationToken, RecordFocus? focus = null);

    /// <summary>
    /// Shows a recognized text that is not in the history: its saving failed.
    /// </summary>
    /// <param name="transcript">The recognized text.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    Task ShowAsync(Transcript transcript);
}
