using FluentAssertions;
using Microsoft.Data.Sqlite;
using Voicer.Core.History;
using Voicer.Core.Transcripts;

namespace Voicer.Storage.Tests;

/// <summary>
/// Tests of the sound kept with a text: it comes back byte for byte, it gives the record its length, it is erased with
/// the record, and a database of the first version gets its table.
/// </summary>
public sealed class SqliteRecordAudioTests : IDisposable
{
    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-audio-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteRecordAudioTests"/> class.
    /// </summary>
    public SqliteRecordAudioTests()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Gets the database file of the test.
    /// </summary>
    private string Database => Path.Combine(_folder, "voicer.db");

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// The kept sound is written back into a file exactly as it was, with its length.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Audio_ComesBackAsItWas()
    {
        var bytes = Enumerable.Range(0, 300_000).Select(i => (byte)(i * 7)).ToArray();
        var history = new SqliteTranscriptHistory(Database);
        var id = await history.AddAsync(Talk(), Audio(bytes, TimeSpan.FromSeconds(95)), CancellationToken.None);
        var copy = Path.Combine(_folder, "copy.ogg");

        var duration = await history.ExportAudioAsync(id, copy, CancellationToken.None);

        duration.Should().Be(TimeSpan.FromSeconds(95));
        File.ReadAllBytes(copy).Should().Equal(bytes);
    }

    /// <summary>
    /// A record with a sound lasts as long as its sound and says it has one; a record without has none to give.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Audio_GivesTheRecordItsLength()
    {
        var history = new SqliteTranscriptHistory(Database);
        var withSound = await history.AddAsync(Talk(), Audio([1, 2, 3], TimeSpan.FromSeconds(95)), CancellationToken.None);
        var withoutSound = await history.AddAsync(Talk(), null, CancellationToken.None);

        var entries = await history.ListAsync(CancellationToken.None);

        entries.Single(e => e.Id == withSound).Should().Match<HistoryEntry>(e => e.HasAudio && e.Duration == TimeSpan.FromSeconds(95));
        entries.Single(e => e.Id == withoutSound).Should().Match<HistoryEntry>(e => !e.HasAudio && e.Duration == TimeSpan.FromSeconds(2));
        (await history.ExportAudioAsync(withoutSound, Path.Combine(_folder, "none.ogg"), CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// The sound of a deleted record is no longer anywhere in the file.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_ErasesTheSound()
    {
        var marker = "sound-marker-of-a-deleted-record"u8.ToArray();
        var bytes = Enumerable.Repeat(marker, 2000).SelectMany(b => b).ToArray();
        var history = new SqliteTranscriptHistory(Database);
        var id = await history.AddAsync(Talk(), Audio(bytes, TimeSpan.FromSeconds(3)), CancellationToken.None);

        await history.DeleteAsync(id, CancellationToken.None);

        File.ReadAllBytes(Database).AsSpan().IndexOf(marker).Should().Be(-1);
        (await history.ExportAudioAsync(id, Path.Combine(_folder, "gone.ogg"), CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// A database of the first version keeps its records and gets the table of the sound.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task FirstVersion_IsUpgraded()
    {
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA auto_vacuum = INCREMENTAL;
                CREATE TABLE records (id INTEGER PRIMARY KEY, source_path TEXT NOT NULL, source_name TEXT NOT NULL,
                    model TEXT NOT NULL, device TEXT NOT NULL, language TEXT NULL, created_at_ms INTEGER NOT NULL,
                    utc_offset_min INTEGER NOT NULL, duration_ms INTEGER NOT NULL, preview TEXT NOT NULL);
                CREATE TABLE segments (record_id INTEGER NOT NULL REFERENCES records (id) ON DELETE CASCADE,
                    position INTEGER NOT NULL, start_ms INTEGER NOT NULL, end_ms INTEGER NOT NULL, text TEXT NOT NULL,
                    PRIMARY KEY (record_id, position)) WITHOUT ROWID;
                INSERT INTO records VALUES (1, 'C:/old.mp3', 'old.mp3', 'Whisper Large v3 F16', 'CPU', 'en', 0, 0, 2000, 'Old.');
                INSERT INTO segments VALUES (1, 0, 0, 2000, 'Old.');
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }

        var history = new SqliteTranscriptHistory(Database);

        (await history.FindAsync(1, CancellationToken.None))!.Text.Should().Be("Old.");
        (await new SqliteSearchIndex(Database).SearchWordsAsync(Voicer.Core.Search.FullTextQuery.Build("old")!, 10, CancellationToken.None))
            .Should().ContainSingle().Which.RecordId.Should().Be(1);
        (await history.ListAsync(CancellationToken.None)).Single().HasAudio.Should().BeFalse();
        var id = await history.AddAsync(Talk(), Audio([9], TimeSpan.FromSeconds(1)), CancellationToken.None);
        (await history.ExportAudioAsync(id, Path.Combine(_folder, "new.ogg"), CancellationToken.None)).Should().Be(TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Creates a recognized text of two seconds.
    /// </summary>
    /// <returns>The transcript.</returns>
    private static Transcript Talk()
    {
        return new Transcript("talk.mp4", "Whisper Small Q5", "CPU", "en", DateTimeOffset.UnixEpoch, [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), "Hello.")]);
    }

    /// <summary>
    /// Writes a sound file for a record.
    /// </summary>
    /// <param name="bytes">The content of the file.</param>
    /// <param name="duration">How long the sound lasts.</param>
    /// <returns>The sound to keep.</returns>
    private RecordAudio Audio(byte[] bytes, TimeSpan duration)
    {
        var file = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".ogg");
        File.WriteAllBytes(file, bytes);

        return new RecordAudio(file, duration);
    }
}
