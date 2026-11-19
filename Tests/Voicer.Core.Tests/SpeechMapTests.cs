using FluentAssertions;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the joining of the stretches of speech and of the way back to the times of the file.
/// </summary>
public sealed class SpeechMapTests
{
    /// <summary>
    /// A small sample rate, so the samples are easy to count.
    /// </summary>
    private const int RATE = 10;

    /// <summary>
    /// The stretches are joined in order with a pause between them; the silence around them is left out.
    /// </summary>
    [Fact]
    public void Compact_JoinsTheSpeechWithPauses()
    {
        var samples = Enumerable.Range(0, 100).Select(i => (float)i).ToArray();
        var map = SpeechMap.Create([Span(1, 2), Span(5, 6.5)], samples.Length, RATE, TimeSpan.FromSeconds(0.2));

        var joined = map.Compact(samples);

        map.CompactLength.Should().Be(10 + 2 + 15);
        joined.Take(10).Should().Equal(Enumerable.Range(10, 10).Select(i => (float)i));
        joined.Skip(10).Take(2).Should().Equal(0f, 0f);
        joined.Skip(12).Should().Equal(Enumerable.Range(50, 15).Select(i => (float)i));
    }

    /// <summary>
    /// A moment of the joined sound goes back to its moment in the file; a moment in a pause goes to the end of the
    /// stretch before it.
    /// </summary>
    [Fact]
    public void ToOriginal_GoesBackToTheTimesOfTheFile()
    {
        var map = SpeechMap.Create([Span(1, 2), Span(5, 6.5)], 100, RATE, TimeSpan.FromSeconds(0.2));

        map.ToOriginal(TimeSpan.Zero).Should().Be(TimeSpan.FromSeconds(1));
        map.ToOriginal(TimeSpan.FromSeconds(0.5)).Should().Be(TimeSpan.FromSeconds(1.5));
        map.ToOriginal(TimeSpan.FromSeconds(1.1)).Should().Be(TimeSpan.FromSeconds(2));
        map.ToOriginal(TimeSpan.FromSeconds(1.2)).Should().Be(TimeSpan.FromSeconds(5));
        map.ToOriginal(TimeSpan.FromSeconds(2.7)).Should().Be(TimeSpan.FromSeconds(6.5));
        map.ToOriginal(TimeSpan.FromSeconds(9)).Should().Be(TimeSpan.FromSeconds(6.5));
    }

    /// <summary>
    /// Stretches out of order, overlapping or beyond the end of the sound are sorted, merged and cut to the sound.
    /// </summary>
    [Fact]
    public void Create_SortsMergesAndCutsTheStretches()
    {
        var map = SpeechMap.Create([Span(8, 12), Span(3, 5), Span(4, 6), Span(7, 7)], 100, RATE, TimeSpan.FromSeconds(1));

        map.CompactLength.Should().Be(30 + 10 + 20);
        map.ToOriginal(TimeSpan.FromSeconds(2.9)).Should().Be(TimeSpan.FromSeconds(5.9));
        map.ToOriginal(TimeSpan.FromSeconds(4)).Should().Be(TimeSpan.FromSeconds(8));
        map.ToOriginal(TimeSpan.FromSeconds(6)).Should().Be(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// A sound without speech gives an empty map and an empty joined sound.
    /// </summary>
    [Fact]
    public void Create_WithoutSpeech_IsEmpty()
    {
        var map = SpeechMap.Create([], 100, RATE, TimeSpan.FromSeconds(0.2));

        map.IsEmpty.Should().BeTrue();
        map.Compact(new float[100]).Should().BeEmpty();
    }

    /// <summary>
    /// Makes a stretch of speech.
    /// </summary>
    /// <param name="start">Where it begins, in seconds.</param>
    /// <param name="end">Where it ends, in seconds.</param>
    /// <returns>The stretch.</returns>
    private static SpeechSpan Span(double start, double end)
    {
        return new SpeechSpan(TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end));
    }
}
