using FluentAssertions;
using Voicer.Core.Corrections;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the corrections of a record: how they change its text and mark the changed places, what a click or a
/// selection corrects, and how a new correction joins the ones before it.
/// </summary>
public sealed class TranscriptCorrectionsTests
{
    /// <summary>
    /// The moment of the corrections of the tests.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 10, 0, 0, TimeSpan.FromHours(3));

    /// <summary>
    /// The length of the line break between the phrases of a text.
    /// </summary>
    private static readonly int _newLine = Environment.NewLine.Length;

    /// <summary>
    /// A corrected word takes the place of the recognized one and is marked there; the phrase keeps its moments.
    /// </summary>
    [Fact]
    public void Apply_ReplacesTheWords_AndMarksThem()
    {
        var original = Record((0, 2, " Привет мир"), (2, 4, "Как дила?"));
        var correction = Correction(1, 4, 4, "дила", "дела");

        var result = TranscriptCorrections.Apply(original, [correction]);

        result.Transcript.Text.Should().Be("Привет мир" + Environment.NewLine + "Как дела?");
        result.Transcript.Segments[1].Start.Should().Be(original.Segments[1].Start);
        result.Transcript.Segments[1].End.Should().Be(original.Segments[1].End);
        result.Marks.Should().ContainSingle();
        result.Marks[0].Range.Should().Be(new TextMark(10 + _newLine + 4, 4));
        result.Marks[0].Correction.Should().Be(correction);
    }

    /// <summary>
    /// Two corrections of one phrase are both applied and marked where they ended up.
    /// </summary>
    [Fact]
    public void Apply_TwoCorrectionsInOnePhrase_KeepTheirPlaces()
    {
        var original = Record((0, 2, " раз два три"));

        var result = TranscriptCorrections.Apply(original, [Correction(0, 8, 3, "три", "3"), Correction(0, 0, 3, "раз", "один")]);

        result.Transcript.Text.Should().Be("один два 3");
        result.Marks.Select(m => m.Range).Should().Equal(new TextMark(0, 4), new TextMark(9, 1));
    }

    /// <summary>
    /// A deleted word is marked by one character where it was, so its trace can be seen and pointed at.
    /// </summary>
    [Fact]
    public void Apply_Deletion_MarksOneCharacterWhereTheWordWas()
    {
        var original = Record((0, 2, "раз два три"));

        var result = TranscriptCorrections.Apply(original, [Correction(0, 3, 4, " два", string.Empty)]);

        result.Transcript.Text.Should().Be("раз три");
        result.Marks.Should().ContainSingle().Which.Range.Should().Be(new TextMark(3, 1));
    }

    /// <summary>
    /// A deleted word at the end of a phrase is marked by the last character of the phrase, not by the line break.
    /// </summary>
    [Fact]
    public void Apply_DeletionAtTheEnd_StaysInThePhrase()
    {
        var original = Record((0, 2, "раз два"), (2, 4, "три"));

        var result = TranscriptCorrections.Apply(original, [Correction(0, 3, 4, " два", string.Empty)]);

        result.Transcript.Text.Should().Be("раз" + Environment.NewLine + "три");
        result.Marks.Should().ContainSingle().Which.Range.Should().Be(new TextMark(2, 1));
    }

    /// <summary>
    /// Corrections read from the file that do not fit the text are left out instead of breaking the record.
    /// </summary>
    [Fact]
    public void Apply_SkipsCorrectionsThatDoNotFit()
    {
        var original = Record((0, 2, "раз два три"));
        var fitting = Correction(0, 4, 3, "два", "2");

        var result = TranscriptCorrections.Apply(
            original,
            [
                Correction(0, 0, 3, "сто", "100"),
                Correction(5, 0, 3, "раз", "1"),
                Correction(0, 8, 9, "три", "3"),
                Correction(0, -1, 2, "ра", "1"),
                fitting,
                Correction(0, 5, 2, "ва", "!"),
            ]);

        result.Transcript.Text.Should().Be("раз 2 три");
        result.Marks.Should().ContainSingle().Which.Correction.Should().Be(fitting);
    }

    /// <summary>
    /// A word is taken whole, with a hyphen or an apostrophe inside it; punctuation, spaces and the end of the text are no
    /// word.
    /// </summary>
    [Fact]
    public void WordAt_TakesTheWholeWord()
    {
        const string TEXT = "Ну, по-русски — don't.";

        TranscriptCorrections.WordAt(TEXT, 5).Should().Be(new TextMark(4, 9));
        TranscriptCorrections.WordAt(TEXT, 17).Should().Be(new TextMark(16, 5));
        TranscriptCorrections.WordAt(TEXT, 2).Should().BeNull();
        TranscriptCorrections.WordAt(TEXT, 14).Should().BeNull();
        TranscriptCorrections.WordAt(TEXT, TEXT.Length).Should().BeNull();
        TranscriptCorrections.WordAt(TEXT, -1).Should().BeNull();
    }

    /// <summary>
    /// A click on the line break between two phrases corrects nothing.
    /// </summary>
    [Fact]
    public void Target_OnANewLine_IsNothing()
    {
        var text = new RecordText(Record((0, 2, "раз два"), (2, 4, "три")), []);

        TranscriptCorrections.Target(text, 7).Should().BeNull();
        TranscriptCorrections.Target(text, 7, new TextMark(7, _newLine)).Should().BeNull();
    }

    /// <summary>
    /// A click on a word corrects that word.
    /// </summary>
    [Fact]
    public void Target_OfAClick_IsTheWord()
    {
        var text = new RecordText(Record((0, 2, " раз два"), (2, 4, "три")), []);

        var target = TranscriptCorrections.Target(text, 5);

        target.Should().Be(new CorrectionTarget(0, 4, 3, new TextMark(4, 3), "два"));
    }

    /// <summary>
    /// A click inside a corrected place takes the whole correction, and the field shows what is there now.
    /// </summary>
    [Fact]
    public void Target_InsideACorrection_TakesItWhole()
    {
        var text = new RecordText(Record((0, 2, " раз два")), [Correction(0, 4, 3, "два", "двести")]);

        var target = TranscriptCorrections.Target(text, 7);

        target.Should().Be(new CorrectionTarget(0, 4, 3, new TextMark(4, 6), "двести"));
    }

    /// <summary>
    /// A selection that runs over two phrases is cut to the phrase that was clicked; spaces at its edges are left out.
    /// </summary>
    [Fact]
    public void Target_SelectionAcrossPhrases_StaysInThePhraseOfTheClick()
    {
        var text = new RecordText(Record((0, 2, "раз два "), (2, 4, "три")), []);
        var selection = new TextMark(3, 7 + _newLine);

        TranscriptCorrections.Target(text, 5, selection).Should().Be(new CorrectionTarget(0, 4, 3, new TextMark(4, 3), "два"));
        TranscriptCorrections.Target(text, 7 + _newLine + 1, selection)
            .Should().Be(new CorrectionTarget(1, 0, 3, new TextMark(7 + _newLine, 3), "три"));
    }

    /// <summary>
    /// A selection that starts on the line break, asked from the keyboard, is the phrase it reaches.
    /// </summary>
    [Fact]
    public void Target_SelectionFromTheLineBreak_TakesThePhraseItReaches()
    {
        var text = new RecordText(Record((0, 2, "раз два"), (2, 4, "три")), []);

        var target = TranscriptCorrections.Target(text, 7, new TextMark(7, _newLine + 3));

        target.Should().Be(new CorrectionTarget(1, 0, 3, new TextMark(7 + _newLine, 3), "три"));
    }

    /// <summary>
    /// After deletions at the beginning of a phrase leave a space there, the next word is still corrected in its own place.
    /// </summary>
    [Fact]
    public void Put_AfterDeletionsAtTheBeginning_ReplacesTheRightWord()
    {
        var text = new RecordText(Record((0, 2, " aa bb cc")), []);
        text = text with { Corrections = TranscriptCorrections.Put(text, TranscriptCorrections.Target(text, 3)!, string.Empty, _now)! };
        text = text with { Corrections = TranscriptCorrections.Put(text, TranscriptCorrections.Target(text, 0)!, string.Empty, _now)! };
        text.Apply().Transcript.Text.Should().Be("cc");

        var corrections = TranscriptCorrections.Put(text, TranscriptCorrections.Target(text, 0)!, "dd", _now)!;

        new RecordText(text.Original, corrections).Apply().Transcript.Text.Should().Be("dd");
    }

    /// <summary>
    /// A double space left at the beginning of a phrase by a deletion does not move the next correction.
    /// </summary>
    [Fact]
    public void Put_AfterADeletionBeforeADoubleSpace_ReplacesTheRightWord()
    {
        var text = new RecordText(Record((0, 2, "aa  bb")), []);
        text = text with { Corrections = TranscriptCorrections.Put(text, TranscriptCorrections.Target(text, 0)!, string.Empty, _now)! };

        var corrections = TranscriptCorrections.Put(text, TranscriptCorrections.Target(text, 0)!, "XX", _now)!;

        new RecordText(text.Original, corrections).Apply().Transcript.Text.Should().Be("XX");
    }

    /// <summary>
    /// A correction over another one replaces both, and the original stays the recognized text.
    /// </summary>
    [Fact]
    public void Put_OverAnotherCorrection_MergesThem_AndKeepsTheWhisperText()
    {
        var text = new RecordText(Record((0, 2, " раз два")), [Correction(0, 4, 3, "два", "двести")]);
        var target = TranscriptCorrections.Target(text, 0, new TextMark(0, 10))!;

        var corrections = TranscriptCorrections.Put(text, target, " триста ", _now);

        corrections.Should().Equal(Correction(0, 0, 7, "раз два", "триста"));
    }

    /// <summary>
    /// Putting the recognized text back removes the correction.
    /// </summary>
    [Fact]
    public void Put_TheOriginalBack_RemovesTheCorrection()
    {
        var text = new RecordText(Record((0, 2, "раз два")), [Correction(0, 4, 3, "два", "двести")]);
        var target = TranscriptCorrections.Target(text, 5)!;

        TranscriptCorrections.Put(text, target, "два", _now).Should().BeEmpty();
    }

    /// <summary>
    /// An empty replacement deletes the word with one space next to it, so no double space is left.
    /// </summary>
    [Fact]
    public void Put_Empty_DeletesTheWordWithASpace()
    {
        var text = new RecordText(Record((0, 2, "раз два три")), []);
        var target = TranscriptCorrections.Target(text, 5)!;

        var corrections = TranscriptCorrections.Put(text, target, string.Empty, _now)!;

        corrections.Should().Equal(Correction(0, 3, 4, " два", string.Empty));
        new RecordText(text.Original, corrections).Apply().Transcript.Text.Should().Be("раз три");
    }

    /// <summary>
    /// The first word is deleted with the space after it.
    /// </summary>
    [Fact]
    public void Put_EmptyFirstWord_TakesTheSpaceAfterIt()
    {
        var text = new RecordText(Record((0, 2, "раз два")), []);
        var target = TranscriptCorrections.Target(text, 0)!;

        TranscriptCorrections.Put(text, target, string.Empty, _now).Should().Equal(Correction(0, 0, 4, "раз ", string.Empty));
    }

    /// <summary>
    /// A phrase cannot be left without text: its trace would be gone, and with it the way back.
    /// </summary>
    [Fact]
    public void Put_EmptyWholePhrase_IsRefused()
    {
        var text = new RecordText(Record((0, 2, " раз"), (2, 4, "два")), []);
        var target = TranscriptCorrections.Target(text, 0)!;

        TranscriptCorrections.Put(text, target, "  ", _now).Should().BeNull();
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

    /// <summary>
    /// Builds a correction made at the moment of the tests.
    /// </summary>
    /// <param name="segment">The index of the phrase.</param>
    /// <param name="start">Where the replaced piece starts in the trimmed recognized text of the phrase.</param>
    /// <param name="length">How long the replaced piece is.</param>
    /// <param name="original">The replaced piece.</param>
    /// <param name="replacement">What replaces it.</param>
    /// <returns>The correction.</returns>
    private static TextCorrection Correction(int segment, int start, int length, string original, string replacement)
    {
        return new TextCorrection(segment, start, length, original, replacement, _now);
    }
}
