using FluentAssertions;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of what the search marks in a found text: the beginnings of the words of the query, the matching words, the
/// excerpt of a long passage and the passage in the text of its record.
/// </summary>
public sealed class SearchMarksTests
{
    /// <summary>
    /// The beginnings are the ones the full-text index looks up: words by their forms, phrases word for word.
    /// </summary>
    [Fact]
    public void Prefixes_AreTheBeginningsTheIndexLooksUp()
    {
        FullTextQuery.Prefixes("Договоров \"Итоги встречи\" план план").Should().Equal("договор", "итоги", "встречи", "план");
        FullTextQuery.Prefixes("  ?!  ").Should().BeEmpty();
        FullTextQuery.Prefixes(null).Should().BeEmpty();
    }

    /// <summary>
    /// Every form of a word of the query is marked, whatever its case; a word that only contains the beginning is not.
    /// </summary>
    [Fact]
    public void Words_MarksTheFormsOfTheWords()
    {
        var marked = SearchMarks.Words("Договор подписан. По договору, как и в недоговорённости.", FullTextQuery.Prefixes("договоров"));

        marked.Words.Should().Equal(new TextMark(0, 7), new TextMark(21, 8));
        marked.Text.Substring(21, 8).Should().Be("договору");
        marked.Passage.Should().BeNull();
        marked.HasMarks.Should().BeTrue();
    }

    /// <summary>
    /// A query without words marks nothing.
    /// </summary>
    [Fact]
    public void Words_WithoutAQuery_MarksNothing()
    {
        var marked = SearchMarks.Words("Текст.", []);

        marked.Words.Should().BeEmpty();
        marked.HasMarks.Should().BeFalse();
    }

    /// <summary>
    /// A word far from the beginning is shown with a few words before it, cut at the beginning of a word.
    /// </summary>
    [Fact]
    public void Excerpt_StartsNearTheFirstWord()
    {
        var text = string.Concat(Enumerable.Repeat("слово ", 40)) + "бюджет утверждён";
        var marked = SearchMarks.Words(text, FullTextQuery.Prefixes("бюджет"));

        var excerpt = SearchMarks.Excerpt(marked, 20);

        excerpt.Text.Should().StartWith("…слово ").And.EndWith("бюджет утверждён");
        excerpt.Text.Length.Should().BeLessThanOrEqualTo(1 + 20 + "бюджет утверждён".Length);
        var word = excerpt.Words.Single();
        excerpt.Text.Substring(word.Start, word.Length).Should().Be("бюджет");
    }

    /// <summary>
    /// A word close to the beginning, or no word at all, leaves the text as it is.
    /// </summary>
    [Fact]
    public void Excerpt_KeepsAShortLead()
    {
        var near = SearchMarks.Words("Итак, бюджет утверждён", FullTextQuery.Prefixes("бюджет"));
        var none = SearchMarks.Words(new string('а', 500), []);

        SearchMarks.Excerpt(near, 20).Should().BeSameAs(near);
        SearchMarks.Excerpt(none, 20).Should().BeSameAs(none);
    }

    /// <summary>
    /// The passage is found by the moments of its pieces of speech, in the text the record shows: empty pieces take no
    /// line, and a neighbour that only touches the passage is not taken in.
    /// </summary>
    [Fact]
    public void Passage_IsFoundInTheTextOfTheRecord()
    {
        TranscriptSegment[] segments =
        [
            new(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), " Первый. "),
            new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6), "  "),
            new(TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(9), "Второй."),
            new(TimeSpan.FromSeconds(9), TimeSpan.FromSeconds(12), "Третий."),
            new(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15), "Четвёртый."),
        ];
        var transcript = Record(segments);

        var passage = SearchMarks.Passage(transcript, TimeSpan.FromSeconds(5.001), TimeSpan.FromSeconds(12));

        passage.Should().NotBeNull();
        transcript.Text.Substring(passage!.Value.Start, passage.Value.Length).Should().Be("Второй." + Environment.NewLine + "Третий.");
    }

    /// <summary>
    /// Moments where no speech lies give no passage.
    /// </summary>
    [Fact]
    public void Passage_OutsideTheSpeech_IsNotFound()
    {
        var transcript = Record([new(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5), "Первый.")]);

        SearchMarks.Passage(transcript, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(40)).Should().BeNull();
    }

    /// <summary>
    /// Creates a record of the given pieces of speech.
    /// </summary>
    /// <param name="segments">The pieces of speech.</param>
    /// <returns>The record.</returns>
    private static Transcript Record(IReadOnlyList<TranscriptSegment> segments)
    {
        return new Transcript("talk.mp4", "Whisper", "CPU", "ru", DateTimeOffset.UnixEpoch, segments);
    }
}
