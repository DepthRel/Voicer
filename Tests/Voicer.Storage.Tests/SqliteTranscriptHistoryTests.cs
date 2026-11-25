using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Voicer.Core.Enums;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.Storage.Tests;

/// <summary>
/// Tests of the history in a SQLite file: what is kept, in which order, and that a deleted record leaves no trace.
/// </summary>
public sealed class SqliteTranscriptHistoryTests : IDisposable
{
    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-history-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets the database file of the test.
    /// </summary>
    private string File => Path.Combine(_folder, "voicer.db");

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// A kept transcript is read back whole: its fragments in their order, the moment with its offset, the language.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Add_ThenFind_ReturnsTheSameTranscript()
    {
        var history = new SqliteTranscriptHistory(File);
        var transcript = Transcript("meeting.mp4", new DateTimeOffset(2026, 11, 18, 14, 30, 5, 123, TimeSpan.FromHours(3)), "Привет.", "Как дела?");

        var id = await history.AddAsync(transcript, null, CancellationToken.None);
        var found = await history.FindAsync(id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.SourcePath.Should().Be(transcript.SourcePath);
        found.ModelName.Should().Be(transcript.ModelName);
        found.DeviceName.Should().Be(transcript.DeviceName);
        found.Language.Should().Be("ru");
        found.CreatedAt.Should().Be(transcript.CreatedAt);
        found.CreatedAt.Offset.Should().Be(TimeSpan.FromHours(3));
        found.Segments.Should().Equal(transcript.Segments);
    }

    /// <summary>
    /// The list goes from the newest record to the oldest and describes each without its whole text.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task List_GoesFromTheNewest()
    {
        var history = new SqliteTranscriptHistory(File);
        await history.AddAsync(Transcript("old.mp3", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), "Old."), null, CancellationToken.None);
        await history.AddAsync(Transcript("new.mp3", new DateTimeOffset(2026, 2, 1, 10, 0, 0, TimeSpan.Zero), "New", "text."), null, CancellationToken.None);

        var entries = await history.ListAsync(CancellationToken.None);

        entries.Select(e => e.SourceName).Should().Equal("new.mp3", "old.mp3");
        entries[0].Preview.Should().Be("New text.");
        entries[0].Duration.Should().Be(TimeSpan.FromSeconds(4));
        entries[0].ModelName.Should().Be("Whisper Large v3 Turbo Q8");
    }

    /// <summary>
    /// A transcript without speech is kept too, with no language and no duration.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Add_KeepsATranscriptWithoutSpeech()
    {
        var history = new SqliteTranscriptHistory(File);
        var silent = new Transcript("silence.wav", "Whisper Small Q5", "CPU", null, DateTimeOffset.UnixEpoch, []);

        var id = await history.AddAsync(silent, null, CancellationToken.None);

        var found = await history.FindAsync(id, CancellationToken.None);
        found!.Language.Should().BeNull();
        found.Segments.Should().BeEmpty();
        (await history.ListAsync(CancellationToken.None)).Single().Duration.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// A deleted record is gone with its fragments, and its text is no longer anywhere in the file.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_RemovesTheRecordFromTheFile()
    {
        const string SECRET = "unrepeatable-marker-of-the-deleted-text";
        var history = new SqliteTranscriptHistory(File);
        var kept = await history.AddAsync(Transcript("kept.mp3", DateTimeOffset.UnixEpoch, "Kept."), null, CancellationToken.None);
        var id = await history.AddAsync(Transcript("secret.mp3", DateTimeOffset.UnixEpoch, string.Concat(Enumerable.Repeat(SECRET + " ", 400))), null, CancellationToken.None);

        (await history.DeleteAsync(id, CancellationToken.None)).Should().BeTrue();

        (await history.FindAsync(id, CancellationToken.None)).Should().BeNull();
        (await history.ListAsync(CancellationToken.None)).Select(e => e.Id).Should().Equal(kept);
        CountSegments(id).Should().Be(0);
        System.IO.File.ReadAllBytes(File).AsSpan().IndexOf(Encoding.UTF8.GetBytes(SECRET)).Should().Be(-1);
    }

    /// <summary>
    /// Deleting a record that is not there changes nothing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_OfAMissingRecord_ReturnsFalse()
    {
        var history = new SqliteTranscriptHistory(File);

        (await history.DeleteAsync(42, CancellationToken.None)).Should().BeFalse();
        (await history.FindAsync(42, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// The records outlive the instance: a new one over the same file finds them.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Records_SurviveARestart()
    {
        var id = await new SqliteTranscriptHistory(File).AddAsync(Transcript("talk.wav", DateTimeOffset.UnixEpoch, "Hello."), null, CancellationToken.None);

        var found = await new SqliteTranscriptHistory(File).FindAsync(id, CancellationToken.None);

        found!.Text.Should().Be("Hello.");
    }

    /// <summary>
    /// How a record was made is kept with it: the list and the record itself tell a generation from a recognition.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Kind_IsKeptWithTheRecord()
    {
        var history = new SqliteTranscriptHistory(File);
        var recognized = await history.AddAsync(Transcript("talk.wav", DateTimeOffset.UnixEpoch, "Hello."), null, CancellationToken.None);
        var generated = await history.AddAsync(
            Transcript("Welcome.wav", DateTimeOffset.UnixEpoch.AddMinutes(1), "Welcome.") with { Kind = RecordKind.Generation },
            null,
            CancellationToken.None);

        var list = await history.ListAsync(CancellationToken.None);

        list.Single(e => e.Id == recognized).Kind.Should().Be(RecordKind.Transcription);
        list.Single(e => e.Id == generated).Kind.Should().Be(RecordKind.Generation);
        (await history.FindAsync(generated, CancellationToken.None))!.Kind.Should().Be(RecordKind.Generation);
    }

    /// <summary>
    /// A file of version 4, from before generations, is brought up to date: its records are recognitions.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task FileOfVersion4_IsUpgraded_AndItsRecordsAreRecognitions()
    {
        var id = await new SqliteTranscriptHistory(File).AddAsync(Transcript("talk.wav", DateTimeOffset.UnixEpoch, "Hello."), null, CancellationToken.None);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = File, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE corrections; ALTER TABLE records DROP COLUMN kind; PRAGMA user_version = 4;";
            command.ExecuteNonQuery();
        }

        var list = await new SqliteTranscriptHistory(File).ListAsync(CancellationToken.None);

        list.Should().ContainSingle(e => e.Id == id).Which.Kind.Should().Be(RecordKind.Transcription);
    }

    /// <summary>
    /// The number of a deleted record, even the newest one, never names a new record: a page that still holds it does
    /// not open or change another record.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Ids_OfDeletedRecords_AreNotReused()
    {
        var history = new SqliteTranscriptHistory(File);
        var newest = await history.AddAsync(Transcript("a.mp3", DateTimeOffset.UnixEpoch, "A."), null, CancellationToken.None);
        await history.DeleteAsync(newest, CancellationToken.None);

        var next = await new SqliteTranscriptHistory(File).AddAsync(Transcript("b.mp3", DateTimeOffset.UnixEpoch, "B."), null, CancellationToken.None);

        next.Should().BeGreaterThan(newest);
        (await history.FindAsync(newest, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// A file of the first version, whose records were numbered with reuse, keeps its records and their fragments when
    /// the table is rebuilt, and the fragments still go with their record.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task FileOfVersion1_IsRebuilt_WithoutLosingRows()
    {
        Directory.CreateDirectory(_folder);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = File, Pooling = false }.ToString()))
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
                INSERT INTO records VALUES (1, 'C:/one.mp3', 'one.mp3', 'Whisper Small Q5', 'CPU', 'en', 0, 0, 2000, 'One.');
                INSERT INTO records VALUES (2, 'C:/two.mp3', 'two.mp3', 'Whisper Small Q5', 'CPU', 'en', 1, 0, 2000, 'Two.');
                INSERT INTO segments VALUES (1, 0, 0, 2000, 'One.');
                INSERT INTO segments VALUES (2, 0, 0, 2000, 'Two.');
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }

        var history = new SqliteTranscriptHistory(File);

        (await history.ListAsync(CancellationToken.None)).Select(e => e.Id).Should().Equal(2, 1);
        (await history.FindAsync(1, CancellationToken.None))!.Text.Should().Be("One.");
        (await history.DeleteAsync(2, CancellationToken.None)).Should().BeTrue();
        CountSegments(2).Should().Be(0);
        CountSegments(1).Should().Be(1);
        (await history.AddAsync(Transcript("three.mp3", DateTimeOffset.UnixEpoch, "Three."), null, CancellationToken.None)).Should().Be(3);
    }

    /// <summary>
    /// A file written by a newer build is not touched: the failure says so in the language of the user.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task NewerDatabase_IsRefused()
    {
        Directory.CreateDirectory(_folder);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = File, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            command.ExecuteNonQuery();
        }

        var act = () => new SqliteTranscriptHistory(File).ListAsync(CancellationToken.None);

        var failure = await act.Should().ThrowAsync<IOException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("History.NewerDatabase", File));
    }

    /// <summary>
    /// Creates a transcript with a fragment of two seconds per text.
    /// </summary>
    /// <param name="name">The name of the recognized file.</param>
    /// <param name="createdAt">When it was recognized.</param>
    /// <param name="texts">The texts of the fragments.</param>
    /// <returns>The transcript.</returns>
    private static Transcript Transcript(string name, DateTimeOffset createdAt, params string[] texts)
    {
        var segments = texts.Select((t, i) => new TranscriptSegment(TimeSpan.FromSeconds(i * 2), TimeSpan.FromSeconds((i + 1) * 2), t)).ToList();

        return new Transcript(Path.Combine("C:", "media", name), "Whisper Large v3 Turbo Q8", "RTX 4070", "ru", createdAt, segments);
    }

    /// <summary>
    /// Counts the fragments left of a record, straight in the file.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <returns>The number of fragments.</returns>
    private long CountSegments(long id)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = File, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM segments WHERE record_id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return (long)command.ExecuteScalar()!;
    }
}
