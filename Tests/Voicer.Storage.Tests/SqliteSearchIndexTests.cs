using System.Text;
using FluentAssertions;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Storage.Tests;

/// <summary>
/// Tests of the passages of the records: found by the forms of their words and by their phrases, by the meaning of
/// their vectors, and gone without a trace with their record.
/// </summary>
public sealed class SqliteSearchIndexTests : IDisposable
{
    /// <summary>
    /// The model of the vectors of the tests.
    /// </summary>
    private const string MODEL = "bge-m3";

    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-search-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The history the records are kept in.
    /// </summary>
    private readonly SqliteTranscriptHistory _history;

    /// <summary>
    /// The index under test, over the same file.
    /// </summary>
    private readonly SqliteSearchIndex _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteSearchIndexTests"/> class.
    /// </summary>
    public SqliteSearchIndexTests()
    {
        _history = new SqliteTranscriptHistory(Database);
        _index = new SqliteSearchIndex(Database);
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
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// A word is found in its other forms, with the record and the moment of the passage.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Words_AreFoundInTheirForms()
    {
        var id = await _history.AddAsync(Talk("meeting.mp4", "Обсудили условия договоров с поставщиками.", "Потом пили чай."), null, CancellationToken.None);
        await _history.AddAsync(Talk("other.mp4", "Погода хорошая."), null, CancellationToken.None);

        var hits = await _index.SearchWordsAsync(FullTextQuery.Build("договор")!, 10, CancellationToken.None);

        hits.Should().ContainSingle();
        hits[0].RecordId.Should().Be(id);
        hits[0].SourceName.Should().Be("meeting.mp4");
        hits[0].Start.Should().Be(TimeSpan.Zero);
        hits[0].End.Should().Be(TimeSpan.FromSeconds(4));
        hits[0].Text.Should().Be("Обсудили условия договоров с поставщиками. Потом пили чай.");
    }

    /// <summary>
    /// A phrase in quotes is found only with its words in a row; a passage with more of the words ranks higher.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Phrases_AndRanking()
    {
        await _history.AddAsync(Talk("a.mp4", "Бюджет утвердили, проект запустили."), null, CancellationToken.None);
        await _history.AddAsync(Talk("b.mp4", "Проект без бюджета."), null, CancellationToken.None);

        var phrase = await _index.SearchWordsAsync(FullTextQuery.Build("\"бюджет утвердили\"")!, 10, CancellationToken.None);
        phrase.Select(h => h.SourceName).Should().Equal("a.mp4");

        var words = await _index.SearchWordsAsync(FullTextQuery.Build("утвердили бюджет")!, 10, CancellationToken.None);
        words.Select(h => h.SourceName).Should().Equal("a.mp4", "b.mp4");
    }

    /// <summary>
    /// A word of a script with marks joined to its letters is indexed whole: a word of Hindi finds its passage, and
    /// words that only share its letters without their marks are not found.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Words_WithMarks_AreIndexedWhole()
    {
        await _history.AddAsync(Talk("hindi.mp4", "मुझे हिंदी पसंद है।"), null, CancellationToken.None);
        await _history.AddAsync(Talk("other.mp4", "हाँ दादा आए।"), null, CancellationToken.None);

        var hits = await _index.SearchWordsAsync(FullTextQuery.Build("हिंदी")!, 10, CancellationToken.None);

        hits.Select(h => h.SourceName).Should().Equal("hindi.mp4");
    }

    /// <summary>
    /// Every passage needs a vector of a model until it is given one; the closest vectors come first.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Vectors_AreKeptAndCompared()
    {
        await _history.AddAsync(Talk("a.mp4", "Первый."), null, CancellationToken.None);
        await _history.AddAsync(Talk("b.mp4", "Второй."), null, CancellationToken.None);
        await _history.AddAsync(Talk("c.mp4", "Третий."), null, CancellationToken.None);

        (await _index.CountWithoutVectorAsync(MODEL, CancellationToken.None)).Should().Be(3);
        var passages = await _index.WithoutVectorAsync(MODEL, 10, CancellationToken.None);
        await _index.SaveVectorsAsync(
            MODEL,
            [
                new PassageVector(passages[0].PassageId, VectorMath.Normalize([1, 0, 0])),
                new PassageVector(passages[1].PassageId, VectorMath.Normalize([0.6f, 0.8f, 0])),
                new PassageVector(passages[2].PassageId, VectorMath.Normalize([0, 0, 1])),
                new PassageVector(999, VectorMath.Normalize([1, 1, 1])),
            ],
            CancellationToken.None);

        (await _index.CountWithoutVectorAsync(MODEL, CancellationToken.None)).Should().Be(0);
        (await _index.CountWithoutVectorAsync("other-model", CancellationToken.None)).Should().Be(3);
        var hits = await _index.SearchVectorAsync(MODEL, VectorMath.Normalize([0.8f, 0.6f, 0]), 2, CancellationToken.None);
        hits.Select(h => h.SourceName).Should().Equal("b.mp4", "a.mp4");
    }

    /// <summary>
    /// The passages, their index and their vectors go with their record: neither the words nor the vectors are left in
    /// the file.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_LeavesNoTrace()
    {
        const string WORD = "уникальнейшееслово";
        var id = await _history.AddAsync(Talk("secret.mp4", string.Join(' ', Enumerable.Repeat(WORD, 50))), null, CancellationToken.None);
        var passages = await _index.WithoutVectorAsync(MODEL, 10, CancellationToken.None);
        var marker = Enumerable.Repeat(123.456f, 64).ToArray();
        await _index.SaveVectorsAsync(MODEL, [new PassageVector(passages[0].PassageId, marker)], CancellationToken.None);

        await _history.DeleteAsync(id, CancellationToken.None);

        (await _index.SearchWordsAsync(FullTextQuery.Build(WORD)!, 10, CancellationToken.None)).Should().BeEmpty();
        (await _index.SearchVectorAsync(MODEL, marker, 10, CancellationToken.None)).Should().BeEmpty();
        var file = File.ReadAllBytes(Database);
        file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(WORD[..8])).Should().Be(-1);
        file.AsSpan().IndexOf(System.Runtime.InteropServices.MemoryMarshal.AsBytes(marker.AsSpan()[..8])).Should().Be(-1);
    }

    /// <summary>
    /// Creates a recognized text with a fragment of two seconds per text.
    /// </summary>
    /// <param name="name">The name of the recognized file.</param>
    /// <param name="texts">The texts of the fragments.</param>
    /// <returns>The transcript.</returns>
    private static Transcript Talk(string name, params string[] texts)
    {
        return new Transcript(
            name,
            "Whisper Small Q5",
            "CPU",
            "ru",
            DateTimeOffset.UnixEpoch,
            [.. texts.Select((t, i) => new TranscriptSegment(TimeSpan.FromSeconds(i * 2), TimeSpan.FromSeconds((i + 1) * 2), t))]);
    }
}
