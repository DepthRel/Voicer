using FluentAssertions;
using Voicer.Core.History;
using Voicer.Core.Transcripts;

namespace Voicer.Storage.Tests;

/// <summary>
/// Tests of the summary kept with a record: it comes back, a new one replaces the old, and it goes with the record.
/// </summary>
public sealed class SqliteRecordSummaryTests : IDisposable
{
    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-summary-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The history under test.
    /// </summary>
    private readonly SqliteTranscriptHistory _history;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteRecordSummaryTests"/> class.
    /// </summary>
    public SqliteRecordSummaryTests()
    {
        _history = new SqliteTranscriptHistory(Path.Combine(_folder, "voicer.db"));
    }

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
    /// A kept summary comes back with its model and its moment; a new one replaces it.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summary_IsKeptAndReplaced()
    {
        var id = await _history.AddAsync(Talk(), null, CancellationToken.None);
        var first = new RecordSummary("- point", "qwen3-8b", new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.FromHours(3)));
        var second = first with { Text = "- better point", ModelName = "gemma-3-12b" };

        (await _history.FindSummaryAsync(id, CancellationToken.None)).Should().BeNull();
        (await _history.SaveSummaryAsync(id, first, CancellationToken.None)).Should().BeTrue();
        (await _history.FindSummaryAsync(id, CancellationToken.None)).Should().Be(first);
        (await _history.SaveSummaryAsync(id, second, CancellationToken.None)).Should().BeTrue();
        (await _history.FindSummaryAsync(id, CancellationToken.None)).Should().Be(second);
    }

    /// <summary>
    /// A summary of a record that is not there is not kept.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summary_OfAMissingRecord_IsNotKept()
    {
        var summary = new RecordSummary("text", "model", DateTimeOffset.UnixEpoch);

        (await _history.SaveSummaryAsync(42, summary, CancellationToken.None)).Should().BeFalse();
        (await _history.FindSummaryAsync(42, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>
    /// The summary goes with its record and is no longer in the file.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Summary_GoesWithTheRecord()
    {
        const string MARKER = "summary-marker-of-a-deleted-record";
        var id = await _history.AddAsync(Talk(), null, CancellationToken.None);
        await _history.SaveSummaryAsync(id, new RecordSummary(string.Concat(Enumerable.Repeat(MARKER, 100)), "model", DateTimeOffset.UnixEpoch), CancellationToken.None);

        await _history.DeleteAsync(id, CancellationToken.None);

        (await _history.FindSummaryAsync(id, CancellationToken.None)).Should().BeNull();
        File.ReadAllBytes(Path.Combine(_folder, "voicer.db")).AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(MARKER)).Should().Be(-1);
    }

    /// <summary>
    /// Creates a recognized text.
    /// </summary>
    /// <returns>The transcript.</returns>
    private static Transcript Talk()
    {
        return new Transcript("talk.mp4", "Whisper Small Q5", "CPU", "en", DateTimeOffset.UnixEpoch, [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Hello.")]);
    }
}
