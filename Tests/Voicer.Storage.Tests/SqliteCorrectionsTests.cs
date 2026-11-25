using FluentAssertions;
using Microsoft.Data.Sqlite;
using Voicer.Core.Corrections;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Storage.Tests;

/// <summary>
/// Tests of the corrections of the records: kept over the recognized text without changing it, found by the search,
/// shown in the list, and gone with their record.
/// </summary>
public sealed class SqliteCorrectionsTests : IDisposable
{
    /// <summary>
    /// The moment of the corrections of the tests, with the offset of its clock.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 10, 15, 30, 250, TimeSpan.FromHours(3));

    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-corrections-" + Guid.NewGuid().ToString("N"));

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
    /// The corrections are read back with the recognized text as it was; the text of the record is the corrected one, and
    /// the recognized fragments in the file are untouched.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrections_ThenFindText_KeepsTheWhisperText()
    {
        var history = new SqliteTranscriptHistory(File);
        var talk = Talk(" Обсудили бюжет.", "Потом чай.");
        var id = await history.AddAsync(talk, null, CancellationToken.None);
        var corrections = new[] { new TextCorrection(0, 9, 5, "бюжет", "бюджет", _now) };

        (await history.SaveCorrectionsAsync(id, corrections, CancellationToken.None)).Should().BeTrue();
        var text = await history.FindTextAsync(id, CancellationToken.None);
        var found = await history.FindAsync(id, CancellationToken.None);

        text!.Original.Segments.Should().Equal(talk.Segments);
        text.Corrections.Should().Equal(corrections);
        text.Corrections[0].CreatedAt.Offset.Should().Be(TimeSpan.FromHours(3));
        found!.Text.Should().Be("Обсудили бюджет." + Environment.NewLine + "Потом чай.");
        SegmentTexts(id).Should().Equal(" Обсудили бюжет.", "Потом чай.");
    }

    /// <summary>
    /// The search finds the corrected word and no longer finds the recognized one.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrections_RebuildsTheSearch()
    {
        var history = new SqliteTranscriptHistory(File);
        var index = new SqliteSearchIndex(File);
        var id = await history.AddAsync(Talk("Обсудили бюжет."), null, CancellationToken.None);

        await history.SaveCorrectionsAsync(id, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", _now)], CancellationToken.None);

        (await index.SearchWordsAsync(FullTextQuery.Build("бюджет")!, 10, CancellationToken.None)).Should().ContainSingle(h => h.RecordId == id);
        (await index.SearchWordsAsync(FullTextQuery.Build("бюжет")!, 10, CancellationToken.None)).Should().BeEmpty();
    }

    /// <summary>
    /// The list of the history shows the corrected beginning of the text.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrections_UpdatesThePreview()
    {
        var history = new SqliteTranscriptHistory(File);
        var id = await history.AddAsync(Talk("Обсудили бюжет."), null, CancellationToken.None);

        await history.SaveCorrectionsAsync(id, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", _now)], CancellationToken.None);

        (await history.ListAsync(CancellationToken.None)).Single(e => e.Id == id).Preview.Should().Be("Обсудили бюджет.");
    }

    /// <summary>
    /// Taking the corrections back gives the recognized text back, in the record and in the search.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveNoCorrections_RestoresTheRecognizedText()
    {
        var history = new SqliteTranscriptHistory(File);
        var index = new SqliteSearchIndex(File);
        var id = await history.AddAsync(Talk("Обсудили бюжет."), null, CancellationToken.None);
        await history.SaveCorrectionsAsync(id, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", _now)], CancellationToken.None);

        await history.SaveCorrectionsAsync(id, [], CancellationToken.None);

        (await history.FindTextAsync(id, CancellationToken.None))!.Corrections.Should().BeEmpty();
        (await history.FindAsync(id, CancellationToken.None))!.Text.Should().Be("Обсудили бюжет.");
        (await index.SearchWordsAsync(FullTextQuery.Build("бюжет")!, 10, CancellationToken.None)).Should().ContainSingle();
    }

    /// <summary>
    /// A record deleted meanwhile keeps nothing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrections_OfAMissingRecord_ReturnsFalse()
    {
        var history = new SqliteTranscriptHistory(File);

        (await history.SaveCorrectionsAsync(42, [new TextCorrection(0, 0, 1, "a", "b", _now)], CancellationToken.None)).Should().BeFalse();
        (await history.FindTextAsync(42, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// The corrections go with their record.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_TakesTheCorrectionsAway()
    {
        var history = new SqliteTranscriptHistory(File);
        var id = await history.AddAsync(Talk("Обсудили бюжет."), null, CancellationToken.None);
        await history.SaveCorrectionsAsync(id, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", _now)], CancellationToken.None);

        await history.DeleteAsync(id, CancellationToken.None);

        Count("SELECT COUNT(*) FROM corrections;").Should().Be(0);
    }

    /// <summary>
    /// A file of version 5, from before the corrections, is brought up to date: its records have none.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task FileOfVersion5_IsUpgraded()
    {
        var id = await new SqliteTranscriptHistory(File).AddAsync(Talk("Привет."), null, CancellationToken.None);
        Count("DROP TABLE corrections; PRAGMA user_version = 5; SELECT 0;");

        var text = await new SqliteTranscriptHistory(File).FindTextAsync(id, CancellationToken.None);

        text!.Corrections.Should().BeEmpty();
        Count("PRAGMA user_version;").Should().Be(7);
    }

    /// <summary>
    /// Creates a recognized talk with a fragment of two seconds per text.
    /// </summary>
    /// <param name="texts">The texts of the fragments.</param>
    /// <returns>The transcript.</returns>
    private static Transcript Talk(params string[] texts)
    {
        var segments = texts.Select((t, i) => new TranscriptSegment(TimeSpan.FromSeconds(i * 2), TimeSpan.FromSeconds((i + 1) * 2), t)).ToList();

        return new Transcript(Path.Combine("C:", "media", "talk.mp4"), "Whisper Small", "CPU", "ru", DateTimeOffset.UnixEpoch, segments);
    }

    /// <summary>
    /// Runs SQL straight in the file and reads the number it gives.
    /// </summary>
    /// <param name="sql">The SQL; its last statement gives one number.</param>
    /// <returns>The number.</returns>
    private long Count(string sql)
    {
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (long)command.ExecuteScalar()!;
    }

    /// <summary>
    /// Reads the recognized fragments of a record straight from the file.
    /// </summary>
    /// <param name="id">The identifier of the record.</param>
    /// <returns>The texts of the fragments in their order.</returns>
    private List<string> SegmentTexts(long id)
    {
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT text FROM segments WHERE record_id = $id ORDER BY position;";
        command.Parameters.AddWithValue("$id", id);

        var texts = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            texts.Add(reader.GetString(0));
        }

        return texts;
    }

    /// <summary>
    /// Opens the file of the test without the history.
    /// </summary>
    /// <returns>The open connection.</returns>
    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = File, Pooling = false }.ToString());
        connection.Open();

        return connection;
    }
}
