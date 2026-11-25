using Microsoft.Data.Sqlite;
using Voicer.Localization;

namespace Voicer.Storage;

/// <summary>
/// The SQLite file of the application: opens connections to it and brings its schema up to date on the first one, step
/// by step from the version the file has.
/// </summary>
/// <remarks>
/// The database overwrites what is deleted (<c>secure_delete</c>) and gives the freed pages back to the file system
/// (<c>auto_vacuum = INCREMENTAL</c>), so a deleted record does not stay in the file. Connections are not pooled: the
/// file is closed whenever nothing works with it.
/// </remarks>
internal sealed class HistoryDatabase
{
    /// <summary>
    /// The version of the schema this build writes; it is kept in <c>PRAGMA user_version</c>.
    /// </summary>
    internal const int SCHEMA_VERSION = 7;

    /// <summary>
    /// The schema of version 1: the records of the history and the fragments of their texts.
    /// </summary>
    private const string SCHEMA_V1 = """
        CREATE TABLE records (
            id INTEGER PRIMARY KEY,
            source_path TEXT NOT NULL,
            source_name TEXT NOT NULL,
            model TEXT NOT NULL,
            device TEXT NOT NULL,
            language TEXT NULL,
            created_at_ms INTEGER NOT NULL,
            utc_offset_min INTEGER NOT NULL,
            duration_ms INTEGER NOT NULL,
            preview TEXT NOT NULL
        );
        CREATE INDEX records_by_time ON records (created_at_ms DESC, id DESC);
        CREATE TABLE segments (
            record_id INTEGER NOT NULL REFERENCES records (id) ON DELETE CASCADE,
            position INTEGER NOT NULL,
            start_ms INTEGER NOT NULL,
            end_ms INTEGER NOT NULL,
            text TEXT NOT NULL,
            PRIMARY KEY (record_id, position)
        ) WITHOUT ROWID;
        PRAGMA user_version = 1;
        """;

    /// <summary>
    /// The schema of version 2: the compressed sound of a record, one row per record, gone with it.
    /// </summary>
    private const string SCHEMA_V2 = """
        CREATE TABLE audio (
            record_id INTEGER PRIMARY KEY REFERENCES records (id) ON DELETE CASCADE,
            format TEXT NOT NULL,
            duration_ms INTEGER NOT NULL,
            data BLOB NOT NULL
        );
        PRAGMA user_version = 2;
        """;

    /// <summary>
    /// The schema of version 3: the summary of a record, one per record, gone with it.
    /// </summary>
    private const string SCHEMA_V3 = """
        CREATE TABLE summaries (
            record_id INTEGER PRIMARY KEY REFERENCES records (id) ON DELETE CASCADE,
            model TEXT NOT NULL,
            created_at_ms INTEGER NOT NULL,
            utc_offset_min INTEGER NOT NULL,
            text TEXT NOT NULL
        );
        PRAGMA user_version = 3;
        """;

    /// <summary>
    /// The schema of version 4: the passages of the texts for the search, their full-text index (which overwrites what
    /// is deleted from it) and the vectors of their meaning, per model; all gone with their record.
    /// </summary>
    private const string SCHEMA_V4 = """
        CREATE TABLE passages (
            id INTEGER PRIMARY KEY,
            record_id INTEGER NOT NULL REFERENCES records (id) ON DELETE CASCADE,
            start_ms INTEGER NOT NULL,
            end_ms INTEGER NOT NULL,
            text TEXT NOT NULL
        );
        CREATE INDEX passages_by_record ON passages (record_id);
        CREATE VIRTUAL TABLE passages_fts USING fts5(text, tokenize = 'unicode61 remove_diacritics 2');
        INSERT INTO passages_fts (passages_fts, rank) VALUES ('secure-delete', 1);
        CREATE TRIGGER passages_fts_delete AFTER DELETE ON passages BEGIN
            DELETE FROM passages_fts WHERE rowid = old.id;
        END;
        CREATE TABLE embeddings (
            passage_id INTEGER NOT NULL REFERENCES passages (id) ON DELETE CASCADE,
            model TEXT NOT NULL,
            vector BLOB NOT NULL,
            PRIMARY KEY (passage_id, model)
        ) WITHOUT ROWID;
        """;

    /// <summary>
    /// The schema of version 5: how a record was made, a recognition (0, every record kept before) or a generation (1).
    /// </summary>
    private const string SCHEMA_V5 = """
        ALTER TABLE records ADD COLUMN kind INTEGER NOT NULL DEFAULT 0;
        PRAGMA user_version = 5;
        """;

    /// <summary>
    /// The schema of version 6: the corrections the user made over the recognized text, which stays as it was.
    /// </summary>
    private const string SCHEMA_V6 = """
        CREATE TABLE corrections (
            record_id INTEGER NOT NULL REFERENCES records (id) ON DELETE CASCADE,
            segment INTEGER NOT NULL,
            start INTEGER NOT NULL,
            length INTEGER NOT NULL,
            original TEXT NOT NULL,
            replacement TEXT NOT NULL,
            created_at_ms INTEGER NOT NULL,
            utc_offset_min INTEGER NOT NULL,
            PRIMARY KEY (record_id, segment, start)
        ) WITHOUT ROWID;
        PRAGMA user_version = 6;
        """;

    /// <summary>
    /// The schema of version 7: the records are numbered without reuse, so the number of a deleted record never names a
    /// new one that a page still holding the old number would then change or open. SQLite cannot add
    /// <c>AUTOINCREMENT</c> to a table, so the table is built anew with its rows and its index; the tables that point at
    /// it keep their rows, because the foreign keys are off while it is rebuilt. The full-text index is built anew too,
    /// with the marks joined to letters (<c>M*</c>) counted as parts of words: without them a word of Hindi, Bengali or
    /// Thai fell apart into single letters. It goes after the rename, which checks the trigger that names it.
    /// </summary>
    private const string SCHEMA_V7 = """
        CREATE TABLE records_v7 (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            source_path TEXT NOT NULL,
            source_name TEXT NOT NULL,
            model TEXT NOT NULL,
            device TEXT NOT NULL,
            language TEXT NULL,
            created_at_ms INTEGER NOT NULL,
            utc_offset_min INTEGER NOT NULL,
            duration_ms INTEGER NOT NULL,
            preview TEXT NOT NULL,
            kind INTEGER NOT NULL DEFAULT 0
        );
        INSERT INTO records_v7 (id, source_path, source_name, model, device, language, created_at_ms, utc_offset_min, duration_ms, preview, kind)
            SELECT id, source_path, source_name, model, device, language, created_at_ms, utc_offset_min, duration_ms, preview, kind FROM records;
        DROP TABLE records;
        ALTER TABLE records_v7 RENAME TO records;
        CREATE INDEX records_by_time ON records (created_at_ms DESC, id DESC);
        DROP TABLE passages_fts;
        CREATE VIRTUAL TABLE passages_fts USING fts5(text, tokenize = "unicode61 remove_diacritics 2 categories 'L* N* Co M*'");
        INSERT INTO passages_fts (passages_fts, rank) VALUES ('secure-delete', 1);
        INSERT INTO passages_fts (rowid, text) SELECT id, text FROM passages;
        PRAGMA user_version = 7;
        """;

    /// <summary>
    /// How long a connection waits for a lock of another one, in milliseconds.
    /// </summary>
    private const int BUSY_TIMEOUT_MS = 5000;

    /// <summary>
    /// The connection string of the file.
    /// </summary>
    private readonly string _connectionString;

    /// <summary>
    /// The full path of the file.
    /// </summary>
    private readonly string _file;

    /// <summary>
    /// Lets one connection bring the schema up to date.
    /// </summary>
    private readonly Lock _schemaGate = new();

    /// <summary>
    /// Whether the schema is known to be up to date.
    /// </summary>
    private bool _schemaReady;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryDatabase"/> class.
    /// </summary>
    /// <param name="file">The database file; it and its folder are created when they do not exist.</param>
    internal HistoryDatabase(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        _file = Path.GetFullPath(file);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _file,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    /// <summary>
    /// Opens a connection with the schema up to date.
    /// </summary>
    /// <returns>The open connection; the caller disposes it.</returns>
    /// <exception cref="IOException">The file is newer than this build of the application.</exception>
    /// <exception cref="SqliteException">SQLite failed.</exception>
    internal SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            Execute(connection, $"PRAGMA secure_delete = ON; PRAGMA busy_timeout = {BUSY_TIMEOUT_MS};");
            EnsureSchema(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    /// <summary>
    /// Runs work on a connection away from the calling thread and words a failure of SQLite as an
    /// <see cref="IOException"/>.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Stops the work before it starts.</param>
    /// <returns>The result of the work.</returns>
    internal Task<T> RunAsync<T>(Func<SqliteConnection, T> work, CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    using var connection = Open();

                    return work(connection);
                }
                catch (SqliteException ex)
                {
                    throw LocalizedError.Create(m => new IOException(m, ex), "History.StorageFailed", ex);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Restores a moment with the offset of the clock it was taken by.
    /// </summary>
    /// <param name="unixMilliseconds">The moment, in milliseconds since the Unix epoch.</param>
    /// <param name="offsetMinutes">The offset from UTC, in minutes.</param>
    /// <returns>The moment.</returns>
    internal static DateTimeOffset Moment(long unixMilliseconds, int offsetMinutes)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
    }

    /// <summary>
    /// Gives the pages freed by a deletion back to the file system.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    internal static void ReleaseFreePages(SqliteConnection connection)
    {
        Execute(connection, "PRAGMA incremental_vacuum;");
    }

    /// <summary>
    /// Runs SQL that returns nothing.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="sql">The SQL.</param>
    /// <param name="transaction">The transaction it belongs to, or <see langword="null"/>.</param>
    internal static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates or upgrades the schema, once per instance.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <exception cref="IOException">The file is newer than this build of the application.</exception>
    private void EnsureSchema(SqliteConnection connection)
    {
        lock (_schemaGate)
        {
            if (_schemaReady)
            {
                return;
            }

            var version = ReadVersion(connection);
            if (version > SCHEMA_VERSION)
            {
                throw LocalizedError.Create(m => new IOException(m), "History.NewerDatabase", _file);
            }

            if (version < 1)
            {
                // Only possible before the first table exists: a new file gives freed pages back from the start.
                Execute(connection, "PRAGMA auto_vacuum = INCREMENTAL;");
                Upgrade(connection, SCHEMA_V1);
            }

            if (version < 2)
            {
                Upgrade(connection, SCHEMA_V2);
            }

            if (version < 3)
            {
                Upgrade(connection, SCHEMA_V3);
            }

            if (version < 4)
            {
                UpgradeToPassages(connection);
            }

            if (version < 5)
            {
                Upgrade(connection, SCHEMA_V5);
            }

            if (version < 6)
            {
                Upgrade(connection, SCHEMA_V6);
            }

            if (version < 7)
            {
                UpgradeWithoutForeignKeys(connection, SCHEMA_V7);
            }

            _schemaReady = true;
        }
    }

    /// <summary>
    /// Creates the tables of the search and fills them with the passages of the records already kept, in one transaction.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    private static void UpgradeToPassages(SqliteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, SCHEMA_V4, transaction);

        var records = new List<long>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM records;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                records.Add(reader.GetInt64(0));
            }
        }

        foreach (var record in records)
        {
            TextRows.InsertPassages(connection, transaction, record, TextRows.ReadSegments(connection, transaction, record));
        }

        Execute(connection, "PRAGMA user_version = 4;", transaction);
        transaction.Commit();
    }

    /// <summary>
    /// Applies a step that rebuilds a table other tables point at: the foreign keys are off while it runs, so the rows
    /// that point at the old table are neither deleted nor refused, and on again after it.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="schema">The SQL of the step; it sets the new version.</param>
    private static void UpgradeWithoutForeignKeys(SqliteConnection connection, string schema)
    {
        // The switch has no effect inside a transaction, so it wraps the transaction of the step.
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        try
        {
            Upgrade(connection, schema);
        }
        finally
        {
            Execute(connection, "PRAGMA foreign_keys = ON;");
        }
    }

    /// <summary>
    /// Applies one step of the schema in a transaction of its own.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="schema">The SQL of the step; it sets the new version.</param>
    private static void Upgrade(SqliteConnection connection, string schema)
    {
        using var transaction = connection.BeginTransaction();
        Execute(connection, schema, transaction);
        transaction.Commit();
    }

    /// <summary>
    /// Reads the version of the schema of the file.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <returns>The version; <c>0</c> for a new file.</returns>
    private static long ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        return (long)command.ExecuteScalar()!;
    }
}
