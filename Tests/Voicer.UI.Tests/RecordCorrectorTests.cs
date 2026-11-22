using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.Corrections;
using Voicer.Core.History;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.History;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the corrections of a record in its window: kept in the history first and shown after, or not at all.
/// </summary>
public sealed class RecordCorrectorTests
{
    /// <summary>
    /// The identifier of the record of the tests.
    /// </summary>
    private const long RECORD_ID = 5;

    /// <summary>
    /// The history.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// The clock of the corrections.
    /// </summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// The record as it was recognized.
    /// </summary>
    private readonly Transcript _original = new(
        "talk.mp4",
        "Whisper Small",
        "CPU",
        "ru",
        DateTimeOffset.UnixEpoch,
        [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), " Обсудили бюжет.")]);

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordCorrectorTests"/> class.
    /// </summary>
    public RecordCorrectorTests()
    {
        _history.SaveCorrectionsAsync(RECORD_ID, Arg.Any<IReadOnlyList<TextCorrection>>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    /// <summary>
    /// A correction is kept with the moment of the clock and then shown.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Put_SavesAndShowsTheNewText()
    {
        var corrector = new RecordCorrector(_history, RECORD_ID, new RecordText(_original, []), _time);
        var target = corrector.Target(10)!;

        await corrector.PutAsync(target, "бюджет", CancellationToken.None);

        await _history.Received(1).SaveCorrectionsAsync(
            RECORD_ID,
            Arg.Is<IReadOnlyList<TextCorrection>>(list => list.Single().Replacement == "бюджет" && list.Single().CreatedAt == _time.GetUtcNow()),
            Arg.Any<CancellationToken>());
        corrector.Current.Transcript.Text.Should().Be("Обсудили бюджет.");
        corrector.MarkAt(10)!.Correction.Original.Should().Be("бюжет");
        corrector.MarkAt(2).Should().BeNull();
    }

    /// <summary>
    /// A correction the history could not keep leaves the text as it was.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Put_WhenSavingFails_KeepsTheText()
    {
        _history.SaveCorrectionsAsync(RECORD_ID, Arg.Any<IReadOnlyList<TextCorrection>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk"));
        var corrector = new RecordCorrector(_history, RECORD_ID, new RecordText(_original, []), _time);

        var act = () => corrector.PutAsync(corrector.Target(10)!, "бюджет", CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        corrector.Current.Transcript.Text.Should().Be("Обсудили бюжет.");
    }

    /// <summary>
    /// A record deleted meanwhile is reported as missing, and the text stays as it was.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Put_OfADeletedRecord_Fails()
    {
        _history.SaveCorrectionsAsync(RECORD_ID, Arg.Any<IReadOnlyList<TextCorrection>>(), Arg.Any<CancellationToken>()).Returns(false);
        var corrector = new RecordCorrector(_history, RECORD_ID, new RecordText(_original, []), _time);

        var act = () => corrector.PutAsync(corrector.Target(10)!, "бюджет", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<IOException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Get("History.Missing"));
        corrector.Current.Transcript.Text.Should().Be("Обсудили бюжет.");
    }

    /// <summary>
    /// Taking a correction back keeps the list without it and shows the recognized text.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Revert_RemovesTheCorrection()
    {
        var corrected = new RecordText(_original, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", _time.GetUtcNow())]);
        var corrector = new RecordCorrector(_history, RECORD_ID, corrected, _time);

        await corrector.RevertAsync(corrector.MarkAt(10)!, CancellationToken.None);

        await _history.Received(1).SaveCorrectionsAsync(RECORD_ID, Arg.Is<IReadOnlyList<TextCorrection>>(list => list.Count == 0), Arg.Any<CancellationToken>());
        corrector.Current.Transcript.Text.Should().Be("Обсудили бюжет.");
    }

    /// <summary>
    /// A phrase cannot be emptied; any other replacement can be put.
    /// </summary>
    [Fact]
    public void CanPut_RefusesAnEmptyPhrase()
    {
        var single = new Transcript("a.mp3", "m", "d", "ru", DateTimeOffset.UnixEpoch, [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "Бюжет")]);
        var corrector = new RecordCorrector(_history, RECORD_ID, new RecordText(single, []), _time);
        var target = corrector.Target(0)!;

        corrector.CanPut(target, string.Empty).Should().BeFalse();
        corrector.CanPut(target, "Бюджет").Should().BeTrue();
    }

    /// <summary>
    /// A word corrected right after a deleted one shares its first character with the mark of the deletion; the menu
    /// points at the word, not at the deletion.
    /// </summary>
    [Fact]
    public void MarkAt_PrefersTheCorrectedWordOverADeletion()
    {
        var text = new Transcript("a.mp3", "m", "d", "ru", DateTimeOffset.UnixEpoch, [new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(1), "aa bb")]);
        var corrections = new[]
        {
            new TextCorrection(0, 0, 3, "aa ", string.Empty, _time.GetUtcNow()),
            new TextCorrection(0, 3, 2, "bb", "XX", _time.GetUtcNow()),
        };
        var corrector = new RecordCorrector(_history, RECORD_ID, new RecordText(text, corrections), _time);

        corrector.MarkAt(0)!.Correction.Replacement.Should().Be("XX");
    }
}
