using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Voicer.Core.Search;

namespace Voicer.Storage;

/// <summary>
/// <see cref="IPassageIndex"/> in the SQLite file of the data folder: the words through its full-text index (FTS5, ranked
/// by BM25), the meaning through the vectors kept as blobs and compared in memory, which is fast enough for the
/// thousands of passages of a personal history and needs no native extension.
/// </summary>
public sealed class SqliteSearchIndex : IPassageIndex
{
    /// <summary>
    /// The columns of a found passage, with its record.
    /// </summary>
    private const string HIT_COLUMNS = "p.id, p.record_id, r.source_name, r.created_at_ms, r.utc_offset_min, p.start_ms, p.end_ms, p.text";

    /// <summary>
    /// The passages without a vector of a model.
    /// </summary>
    private const string WITHOUT_VECTOR = "FROM passages p WHERE NOT EXISTS (SELECT 1 FROM embeddings e WHERE e.passage_id = p.id AND e.model = $model)";

    /// <summary>
    /// The database file.
    /// </summary>
    private readonly HistoryDatabase _database;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSearchIndex"/> class.
    /// </summary>
    /// <param name="databaseFile">The database file (<c>Data/voicer.db</c>); it is created on the first use.</param>
    public SqliteSearchIndex(string databaseFile)
    {
        _database = new HistoryDatabase(databaseFile);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PassageHit>> SearchWordsAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return _database.RunAsync<IReadOnlyList<PassageHit>>(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {HIT_COLUMNS}
                    FROM passages_fts f
                    JOIN passages p ON p.id = f.rowid
                    JOIN records r ON r.id = p.record_id
                    WHERE passages_fts MATCH $query
                    ORDER BY bm25(passages_fts)
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$query", query);
                command.Parameters.AddWithValue("$limit", limit);

                return ReadHits(command);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountWithoutVectorAsync(string model, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return _database.RunAsync(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) {WITHOUT_VECTOR};";
                command.Parameters.AddWithValue("$model", model);

                return (int)(long)command.ExecuteScalar()!;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PassageText>> WithoutVectorAsync(string model, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        return _database.RunAsync<IReadOnlyList<PassageText>>(
            connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT p.id, p.text {WITHOUT_VECTOR} ORDER BY p.id LIMIT $limit;";
                command.Parameters.AddWithValue("$model", model);
                command.Parameters.AddWithValue("$limit", limit);

                var passages = new List<PassageText>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    passages.Add(new PassageText(reader.GetInt64(0), reader.GetString(1)));
                }

                return passages;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveVectorsAsync(string model, IReadOnlyList<PassageVector> vectors, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(vectors);

        return _database.RunAsync(
            connection =>
            {
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT OR REPLACE INTO embeddings (passage_id, model, vector)
                    SELECT $id, $model, $vector WHERE EXISTS (SELECT 1 FROM passages WHERE id = $id);
                    """;
                command.Parameters.AddWithValue("$model", model);
                var id = command.Parameters.Add("$id", SqliteType.Integer);
                var vector = command.Parameters.Add("$vector", SqliteType.Blob);

                foreach (var item in vectors)
                {
                    id.Value = item.PassageId;
                    vector.Value = MemoryMarshal.AsBytes(item.Vector.AsSpan()).ToArray();
                    command.ExecuteNonQuery();
                }

                transaction.Commit();

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PassageHit>> SearchVectorAsync(string model, float[] vector, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(vector);

        return _database.RunAsync<IReadOnlyList<PassageHit>>(
            connection =>
            {
                var best = Closest(connection, model, vector, limit);
                if (best.Count == 0)
                {
                    return [];
                }

                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT {HIT_COLUMNS}
                    FROM passages p JOIN records r ON r.id = p.record_id
                    WHERE p.id IN ({string.Join(", ", best.Select((_, i) => "$id" + i))});
                    """;
                for (var i = 0; i < best.Count; i++)
                {
                    command.Parameters.AddWithValue("$id" + i, best[i]);
                }

                var hits = ReadHits(command).ToDictionary(h => h.PassageId);

                return [.. best.Where(hits.ContainsKey).Select(id => hits[id])];
            },
            cancellationToken);
    }

    /// <summary>
    /// Finds the passages whose vectors are the closest to a vector.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="model">The model of the vectors.</param>
    /// <param name="vector">The vector of the query, of unit length.</param>
    /// <param name="limit">How many passages at most.</param>
    /// <returns>The identifiers of the passages, the closest first.</returns>
    private static List<long> Closest(SqliteConnection connection, string model, float[] vector, int limit)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT passage_id, vector FROM embeddings WHERE model = $model;";
        command.Parameters.AddWithValue("$model", model);

        // The best ones so far, the worst of them on top, so a better one replaces it.
        var best = new PriorityQueue<long, float>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var stored = MemoryMarshal.Cast<byte, float>(reader.GetFieldValue<byte[]>(1));
            var similarity = VectorMath.Similarity(vector, stored);
            if (float.IsNegativeInfinity(similarity))
            {
                continue;
            }

            best.Enqueue(reader.GetInt64(0), similarity);
            if (best.Count > limit)
            {
                best.Dequeue();
            }
        }

        var result = new List<long>(best.Count);
        while (best.Count > 0)
        {
            result.Add(best.Dequeue());
        }

        result.Reverse();

        return result;
    }

    /// <summary>
    /// Reads the found passages of a command that selects <see cref="HIT_COLUMNS"/>.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The passages, in the order of the command.</returns>
    private static List<PassageHit> ReadHits(SqliteCommand command)
    {
        var hits = new List<PassageHit>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            hits.Add(new PassageHit(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                HistoryDatabase.Moment(reader.GetInt64(3), reader.GetInt32(4)),
                TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                TimeSpan.FromMilliseconds(reader.GetInt64(6)),
                reader.GetString(7)));
        }

        return hits;
    }
}
