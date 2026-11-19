using Microsoft.Data.Sqlite;
using Voicer.Core.Enums;
using Voicer.Core.History;
using Voicer.Core.Transcripts;

namespace Voicer.Storage;

/// <summary>
/// <see cref="ITranscriptHistory"/> in the SQLite file of the data folder.
/// </summary>
public sealed class SqliteTranscriptHistory : ITranscriptHistory
{
    /// <summary>
    /// How many characters of the text the list of the history shows.
    /// </summary>
    private const int PREVIEW_LENGTH = 240;

    /// <summary>
    /// The format of a kept sound: Opus in an Ogg file.
    /// </summary>
    private const string AUDIO_FORMAT = "ogg-opus";

    /// <summary>
    /// The database file.
    /// </summary>
    private readonly HistoryDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteTranscriptHistory"/> class.
    /// </summary>
    /// <param name="databaseFile">The database file (<c>Data/voicer.db</c>); it is created on the first use.</param>
    public SqliteTranscriptHistory(string databaseFile)
    {
        _database = new HistoryDatabase(databaseFile);
    }

    /// <inheritdoc />
    public Task<long> AddAsync(Transcript transcript, RecordAudio? audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        return _database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                var id = InsertRecord(connection, transaction, transcript, audio?.Duration ?? transcript.Duration);
                InsertSegments(connection, transaction, id, transcript.Segments);
                TextRows.InsertPassages(connection, transaction, id, transcript.Segments);
                if (audio is not null)
                {
                    InsertAudio(connection, transaction, id, audio);
                }

                transaction.Commit();

                return id;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TimeSpan?> ExportAudioAsync(long id, string destination, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        return _database.RunAsync<TimeSpan?>(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT duration_ms FROM audio WHERE record_id = $id;";
                command.Parameters.AddWithValue("$id", id);
                if (command.ExecuteScalar() is not long durationMs)
                {
                    return null;
                }

                // The sound goes from the database to the file in pieces, never whole in memory.
                using var blob = new SqliteBlob(connection, "audio", "data", id, readOnly: true);
                using var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                blob.CopyTo(file);

                return TimeSpan.FromMilliseconds(durationMs);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SaveSummaryAsync(long id, RecordSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO summaries (record_id, model, created_at_ms, utc_offset_min, text)
                    SELECT $id, $model, $created, $offset, $text WHERE EXISTS (SELECT 1 FROM records WHERE id = $id)
                    ON CONFLICT (record_id) DO UPDATE SET
                        model = excluded.model,
                        created_at_ms = excluded.created_at_ms,
                        utc_offset_min = excluded.utc_offset_min,
                        text = excluded.text;
                    """;
                command.Parameters.AddWithValue("$id", id);
                command.Parameters.AddWithValue("$model", summary.ModelName);
                command.Parameters.AddWithValue("$created", summary.CreatedAt.ToUnixTimeMilliseconds());
                command.Parameters.AddWithValue("$offset", (int)summary.CreatedAt.Offset.TotalMinutes);
                command.Parameters.AddWithValue("$text", summary.Text);

                return command.ExecuteNonQuery() > 0;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<RecordSummary?> FindSummaryAsync(long id, CancellationToken cancellationToken)
    {
        return _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT text, model, created_at_ms, utc_offset_min FROM summaries WHERE record_id = $id;";
                command.Parameters.AddWithValue("$id", id);

                using var reader = command.ExecuteReader();

                return reader.Read()
                    ? new RecordSummary(reader.GetString(0), reader.GetString(1), HistoryDatabase.Moment(reader.GetInt64(2), reader.GetInt32(3)))
                    : null;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken cancellationToken)
    {
        return _database.RunAsync<IReadOnlyList<HistoryEntry>>(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id, source_name, model, device, language, created_at_ms, utc_offset_min, duration_ms, preview,
                        EXISTS (SELECT 1 FROM audio WHERE audio.record_id = records.id), kind
                    FROM records
                    ORDER BY created_at_ms DESC, id DESC;
                    """;

                var entries = new List<HistoryEntry>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    entries.Add(new HistoryEntry(
                        reader.GetInt64(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        HistoryDatabase.Moment(reader.GetInt64(5), reader.GetInt32(6)),
                        TimeSpan.FromMilliseconds(reader.GetInt64(7)),
                        reader.GetString(8),
                        reader.GetBoolean(9),
                        KindOf(reader.GetInt64(10))));
                }

                return entries;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Transcript?> FindAsync(long id, CancellationToken cancellationToken)
    {
        return _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT source_path, model, device, language, created_at_ms, utc_offset_min, kind
                    FROM records
                    WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", id);

                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    return null;
                }

                return new Transcript(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    HistoryDatabase.Moment(reader.GetInt64(4), reader.GetInt32(5)),
                    TextRows.ReadSegments(connection, null, id),
                    KindOf(reader.GetInt64(6)));
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        return _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM records WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);

                // The fragments go with the record (ON DELETE CASCADE); secure_delete overwrites them.
                var deleted = command.ExecuteNonQuery() > 0;
                if (deleted)
                {
                    HistoryDatabase.ReleaseFreePages(connection);
                }

                return deleted;
            },
            cancellationToken);
    }

    /// <summary>
    /// Writes the record of a transcript.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction of the addition.</param>
    /// <param name="transcript">The recognized text.</param>
    /// <param name="duration">How long the record lasts: its sound, or its speech without a sound.</param>
    /// <returns>The identifier of the record.</returns>
    private static long InsertRecord(SqliteConnection connection, SqliteTransaction transaction, Transcript transcript, TimeSpan duration)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO records (source_path, source_name, model, device, language, created_at_ms, utc_offset_min, duration_ms, preview, kind)
            VALUES ($path, $name, $model, $device, $language, $created, $offset, $duration, $preview, $kind)
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$kind", (int)transcript.Kind);
        command.Parameters.AddWithValue("$path", transcript.SourcePath);
        command.Parameters.AddWithValue("$name", transcript.SourceName);
        command.Parameters.AddWithValue("$model", transcript.ModelName);
        command.Parameters.AddWithValue("$device", transcript.DeviceName);
        command.Parameters.AddWithValue("$language", (object?)transcript.Language ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", transcript.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$offset", (int)transcript.CreatedAt.Offset.TotalMinutes);
        command.Parameters.AddWithValue("$duration", (long)duration.TotalMilliseconds);
        command.Parameters.AddWithValue("$preview", Preview(transcript.Text));

        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Writes the fragments of a transcript in their order.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction of the addition.</param>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="segments">The fragments.</param>
    private static void InsertSegments(SqliteConnection connection, SqliteTransaction transaction, long id, IReadOnlyList<TranscriptSegment> segments)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO segments (record_id, position, start_ms, end_ms, text)
            VALUES ($id, $position, $start, $end, $text);
            """;
        command.Parameters.AddWithValue("$id", id);
        var position = command.Parameters.Add("$position", SqliteType.Integer);
        var start = command.Parameters.Add("$start", SqliteType.Integer);
        var end = command.Parameters.Add("$end", SqliteType.Integer);
        var text = command.Parameters.Add("$text", SqliteType.Text);

        for (var i = 0; i < segments.Count; i++)
        {
            position.Value = i;
            start.Value = (long)segments[i].Start.TotalMilliseconds;
            end.Value = (long)segments[i].End.TotalMilliseconds;
            text.Value = segments[i].Text;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Writes the sound of a record, streamed from its file into the database.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction of the addition.</param>
    /// <param name="id">The identifier of the record.</param>
    /// <param name="audio">The compressed sound.</param>
    private static void InsertAudio(SqliteConnection connection, SqliteTransaction transaction, long id, RecordAudio audio)
    {
        using var file = new FileStream(audio.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO audio (record_id, format, duration_ms, data)
                VALUES ($id, $format, $duration, zeroblob($length));
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$format", AUDIO_FORMAT);
            command.Parameters.AddWithValue("$duration", (long)audio.Duration.TotalMilliseconds);
            command.Parameters.AddWithValue("$length", file.Length);
            command.ExecuteNonQuery();
        }

        // The row is the record (record_id is its rowid); the blob is filled in pieces, never whole in memory.
        using var blob = new SqliteBlob(connection, "audio", "data", id);
        file.CopyTo(blob);
    }

    /// <summary>
    /// Reads how a record was made; a value this build does not know is taken for a recognition.
    /// </summary>
    /// <param name="value">The value of the column.</param>
    /// <returns>The kind of the record.</returns>
    private static RecordKind KindOf(long value)
    {
        return value == (long)RecordKind.Generation ? RecordKind.Generation : RecordKind.Transcription;
    }

    /// <summary>
    /// Cuts the beginning of a text for the list, on one line.
    /// </summary>
    /// <param name="text">The whole text.</param>
    /// <returns>At most <see cref="PREVIEW_LENGTH"/> characters.</returns>
    private static string Preview(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return line.Length <= PREVIEW_LENGTH ? line : line[..PREVIEW_LENGTH].TrimEnd() + "…";
    }
}
