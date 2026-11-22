using FluentAssertions;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of where the phrases of a record lie in its text and which of them is being said at a moment of its sound.
/// </summary>
public sealed class TranscriptLayoutTests
{
    /// <summary>
    /// Every phrase has its place in the text of the record; an empty one has none and takes no place.
    /// </summary>
    [Fact]
    public void Ranges_FollowTheTextOfTheRecord()
    {
        var transcript = Record((0, 2, " Привет мир"), (2, 4, "   "), (5, 7, "Как дела?"));

        var ranges = TranscriptLayout.Ranges(transcript);

        ranges.Should().HaveCount(3);
        ranges[0].Should().Be(new TextMark(0, 10));
        ranges[1].Should().BeNull();
        ranges[2].Should().Be(new TextMark(10 + Environment.NewLine.Length, 9));
        transcript.Text.Substring(ranges[2]!.Value.Start, 9).Should().Be("Как дела?");
    }

    /// <summary>
    /// The phrase being said is the last one that has begun; an empty phrase and a pause keep the one before.
    /// </summary>
    [Fact]
    public void SegmentAt_FindsThePhraseBeingSaid()
    {
        var transcript = Record((0, 2, "Раз."), (2, 4, " "), (5, 7, "Три."));

        TranscriptLayout.SegmentAt(transcript, TimeSpan.FromSeconds(0.5)).Should().Be(0);
        TranscriptLayout.SegmentAt(transcript, TimeSpan.FromSeconds(3)).Should().Be(0);
        TranscriptLayout.SegmentAt(transcript, TimeSpan.FromSeconds(4.5)).Should().Be(0);
        TranscriptLayout.SegmentAt(transcript, TimeSpan.FromSeconds(5)).Should().Be(2);
        TranscriptLayout.SegmentAt(transcript, TimeSpan.FromSeconds(99)).Should().Be(2);
    }

    /// <summary>
    /// Before the first phrase, and in a record without speech, no phrase is being said.
    /// </summary>
    [Fact]
    public void SegmentAt_BeforeTheFirstPhrase_IsNothing()
    {
        TranscriptLayout.SegmentAt(Record((1, 2, "Раз.")), TimeSpan.FromSeconds(0.5)).Should().BeNull();
        TranscriptLayout.SegmentAt(Record(), TimeSpan.Zero).Should().BeNull();
    }

    /// <summary>
    /// Builds a record of phrases.
    /// </summary>
    /// <param name="phrases">The start and the end in seconds and the text of every phrase.</param>
    /// <returns>The record.</returns>
    private static Transcript Record(params (double Start, double End, string Text)[] phrases)
    {
        var segments = phrases.Select(p => new TranscriptSegment(TimeSpan.FromSeconds(p.Start), TimeSpan.FromSeconds(p.End), p.Text)).ToList();

        return new Transcript("talk.mp3", "Whisper Small", "CPU", "ru", DateTimeOffset.UnixEpoch, segments);
    }
}
