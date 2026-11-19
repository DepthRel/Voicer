using Voicer.Core.Transcripts;

namespace Voicer.Core.History;

/// <summary>
/// The history of the recognitions: every recognized text is kept until the user deletes it. A failure of the storage
/// is reported as an <see cref="IOException"/>.
/// </summary>
public interface ITranscriptHistory
{
    /// <summary>
    /// Keeps a recognized text, with its sound when there is one; both are kept together or not at all.
    /// </summary>
    /// <param name="transcript">The recognized text.</param>
    /// <param name="audio">The compressed sound of the recognized file, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The identifier of the new record.</returns>
    Task<long> AddAsync(Transcript transcript, RecordAudio? audio, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the sound of a record into a file, for the player.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="destination">The file to write; it is replaced.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>How long the sound lasts, or <see langword="null"/> when the record has no sound.</returns>
    Task<TimeSpan?> ExportAudioAsync(long id, string destination, CancellationToken cancellationToken);

    /// <summary>
    /// Keeps the summary of a record, in place of the one it had.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns><see langword="true"/> when the record is there and keeps the summary.</returns>
    Task<bool> SaveSummaryAsync(long id, RecordSummary summary, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the summary of a record.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The summary, or <see langword="null"/> when the record has none.</returns>
    Task<RecordSummary?> FindSummaryAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the records, the newest first.
    /// </summary>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The records.</returns>
    Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the whole text of a record.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The recognized text, or <see langword="null"/> when there is no such record.</returns>
    Task<Transcript?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a record with everything that belongs to it; the deleted data is overwritten, not only forgotten.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns><see langword="true"/> when the record was there and is deleted.</returns>
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken);
}
