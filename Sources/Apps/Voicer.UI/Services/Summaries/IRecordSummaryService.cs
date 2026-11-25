using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.Summaries;

/// <summary>
/// Writes the summary of a record with the model chosen in the settings and keeps it in the history.
/// </summary>
public interface IRecordSummaryService
{
    /// <summary>
    /// Gets a value indicating whether a server and a model are chosen.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Writes the summary of a record and keeps it with the record, in place of the one it had.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="transcript">Its text.</param>
    /// <param name="progress">Receives how far the summary is, and the summary as it arrives.</param>
    /// <param name="cancellationToken">Stops the work; nothing is kept then.</param>
    /// <returns>The kept summary.</returns>
    /// <exception cref="InvalidOperationException">No server or model is chosen.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The text has no speech, or the answer cannot be read.</exception>
    /// <exception cref="IOException">The history could not keep the summary.</exception>
    Task<RecordSummary> SummarizeAsync(long id, Transcript transcript, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the summary of a record from its text as the history keeps it, with the corrections of the user, and
    /// keeps it with the record, in place of the one it had.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="progress">Receives how far the summary is, and the summary as it arrives.</param>
    /// <param name="cancellationToken">Stops the work; nothing is kept then.</param>
    /// <returns>The kept summary.</returns>
    /// <exception cref="InvalidOperationException">No server or model is chosen.</exception>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    /// <exception cref="InvalidDataException">The text has no speech, or the answer cannot be read.</exception>
    /// <exception cref="IOException">The record is gone, or the history could not read it or keep the summary.</exception>
    Task<RecordSummary> SummarizeKeptAsync(long id, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken);
}
