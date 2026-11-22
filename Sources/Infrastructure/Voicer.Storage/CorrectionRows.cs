using Microsoft.Data.Sqlite;
using Voicer.Core.Corrections;

namespace Voicer.Storage;

/// <summary>
/// Reads and writes the corrections of a record, the rows of <c>corrections</c>.
/// </summary>
internal static class CorrectionRows
{
    /// <summary>
    /// Reads the corrections of a record, ordered by phrase and place.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction the reading belongs to, or <see langword="null"/>.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <returns>The corrections.</returns>
    internal static List<TextCorrection> Read(SqliteConnection connection, SqliteTransaction? transaction, long recordId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT segment, start, length, original, replacement, created_at_ms, utc_offset_min
            FROM corrections
            WHERE record_id = $id
            ORDER BY segment, start;
            """;
        command.Parameters.AddWithValue("$id", recordId);

        var corrections = new List<TextCorrection>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            corrections.Add(new TextCorrection(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                HistoryDatabase.Moment(reader.GetInt64(5), reader.GetInt32(6))));
        }

        return corrections;
    }

    /// <summary>
    /// Replaces the corrections of a record.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="transaction">The transaction the writing belongs to.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <param name="corrections">The new corrections of the record.</param>
    internal static void Replace(SqliteConnection connection, SqliteTransaction transaction, long recordId, IReadOnlyList<TextCorrection> corrections)
    {
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM corrections WHERE record_id = $id;";
            delete.Parameters.AddWithValue("$id", recordId);
            delete.ExecuteNonQuery();
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO corrections (record_id, segment, start, length, original, replacement, created_at_ms, utc_offset_min)
            VALUES ($id, $segment, $start, $length, $original, $replacement, $created, $offset);
            """;
        insert.Parameters.AddWithValue("$id", recordId);
        var segment = insert.Parameters.Add("$segment", SqliteType.Integer);
        var start = insert.Parameters.Add("$start", SqliteType.Integer);
        var length = insert.Parameters.Add("$length", SqliteType.Integer);
        var original = insert.Parameters.Add("$original", SqliteType.Text);
        var replacement = insert.Parameters.Add("$replacement", SqliteType.Text);
        var created = insert.Parameters.Add("$created", SqliteType.Integer);
        var offset = insert.Parameters.Add("$offset", SqliteType.Integer);

        foreach (var correction in corrections)
        {
            segment.Value = correction.Segment;
            start.Value = correction.Start;
            length.Value = correction.Length;
            original.Value = correction.Original;
            replacement.Value = correction.Replacement;
            created.Value = correction.CreatedAt.ToUnixTimeMilliseconds();
            offset.Value = (int)correction.CreatedAt.Offset.TotalMinutes;
            insert.ExecuteNonQuery();
        }
    }
}
