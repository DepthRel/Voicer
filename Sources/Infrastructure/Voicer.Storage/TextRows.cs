using Microsoft.Data.Sqlite;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Storage;

/// <summary>
/// Reads the fragments of a record and writes its passages with their full-text index: shared by the history, which
/// keeps new records, and by the upgrade of the schema, which indexes the records kept before the search existed.
/// </summary>
internal static class TextRows
{
    /// <summary>
    /// Reads the fragments of a record in their order.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction the reading belongs to, or <see langword="null"/>.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <returns>The fragments.</returns>
    internal static List<TranscriptSegment> ReadSegments(SqliteConnection connection, SqliteTransaction? transaction, long recordId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT start_ms, end_ms, text FROM segments WHERE record_id = $id ORDER BY position;";
        command.Parameters.AddWithValue("$id", recordId);

        var segments = new List<TranscriptSegment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            segments.Add(new TranscriptSegment(
                TimeSpan.FromMilliseconds(reader.GetInt64(0)),
                TimeSpan.FromMilliseconds(reader.GetInt64(1)),
                reader.GetString(2)));
        }

        return segments;
    }

    /// <summary>
    /// Writes the passages of a record and puts them into the full-text index.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction the writing belongs to.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <param name="segments">The fragments of the record, in their order.</param>
    internal static void InsertPassages(SqliteConnection connection, SqliteTransaction transaction, long recordId, IReadOnlyList<TranscriptSegment> segments)
    {
        using var passage = connection.CreateCommand();
        passage.Transaction = transaction;
        passage.CommandText = """
            INSERT INTO passages (record_id, start_ms, end_ms, text) VALUES ($record, $start, $end, $text)
            RETURNING id;
            """;
        passage.Parameters.AddWithValue("$record", recordId);
        var start = passage.Parameters.Add("$start", SqliteType.Integer);
        var end = passage.Parameters.Add("$end", SqliteType.Integer);
        var text = passage.Parameters.Add("$text", SqliteType.Text);

        using var index = connection.CreateCommand();
        index.Transaction = transaction;
        index.CommandText = "INSERT INTO passages_fts (rowid, text) VALUES ($id, $text);";
        var id = index.Parameters.Add("$id", SqliteType.Integer);
        var indexed = index.Parameters.Add("$text", SqliteType.Text);

        foreach (var built in PassageBuilder.Build(segments))
        {
            start.Value = (long)built.Start.TotalMilliseconds;
            end.Value = (long)built.End.TotalMilliseconds;
            text.Value = built.Text;
            id.Value = (long)passage.ExecuteScalar()!;
            indexed.Value = built.Text;
            index.ExecuteNonQuery();
        }
    }
}
