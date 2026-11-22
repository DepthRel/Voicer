using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.Corrections;
using Voicer.Core.Enums;
using Voicer.Core.History;
using Voicer.Core.Playback;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the window of a record: the phrase being said marked as the sound plays, and the corrections made from the
/// menu of the text.
/// </summary>
public sealed class RecordViewModelTests
{
    /// <summary>
    /// The identifier of the record of the tests.
    /// </summary>
    private const long RECORD_ID = 7;

    /// <summary>
    /// The length of the line break between the phrases of a text.
    /// </summary>
    private static readonly int _newLine = Environment.NewLine.Length;

    /// <summary>
    /// The record: a phrase, an empty one and another phrase after a pause.
    /// </summary>
    private static readonly Transcript _transcript = new(
        "talk.mp4",
        "Whisper Small",
        "CPU",
        "ru",
        DateTimeOffset.UnixEpoch,
        [
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), " Обсудили бюжет."),
            new TranscriptSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), "  "),
            new TranscriptSegment(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7), "Потом чай."),
        ]);

    /// <summary>
    /// The sound of the record.
    /// </summary>
    private readonly IAudioPlayer _audio = Substitute.For<IAudioPlayer>();

    /// <summary>
    /// The history the corrections are kept in.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordViewModelTests"/> class.
    /// </summary>
    public RecordViewModelTests()
    {
        _audio.Duration.Returns(TimeSpan.FromSeconds(7));
        _audio.Speed.Returns(1);
        _audio.Volume.Returns(1);
        _audio.State.Returns(PlaybackState.Paused);
        _history.SaveCorrectionsAsync(RECORD_ID, Arg.Any<IReadOnlyList<TextCorrection>>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    /// <summary>
    /// As the sound plays, the phrase being said is marked; a pause keeps the phrase before it.
    /// </summary>
    [Fact]
    public void Phrase_FollowsThePlayer()
    {
        var view = Window();

        Play(view, 0.5);
        view.Phrase.Should().Be(new TextMark(0, 15));
        view.Marked.Phrase.Should().Be(new TextMark(0, 15));

        Play(view, 4.5);
        view.Phrase.Should().Be(new TextMark(0, 15));

        Play(view, 5.5);
        view.Phrase.Should().Be(new TextMark(15 + _newLine, 10));
    }

    /// <summary>
    /// Nothing is marked as said before the sound is played for the first time.
    /// </summary>
    [Fact]
    public void Phrase_WaitsForTheSound()
    {
        var view = Window();

        view.Player!.Refresh();
        view.FollowPlayer();

        view.Phrase.Should().BeNull();
        view.Marked.Phrase.Should().BeNull();
    }

    /// <summary>
    /// The passage found by the search is marked until the sound starts; the found words stay marked.
    /// </summary>
    [Fact]
    public void FoundPassage_GoesAwayWhenTheSoundStarts()
    {
        var view = Window(focus: new RecordFocus(TimeSpan.Zero, TimeSpan.FromSeconds(2), "бюжет"));
        view.Marked.Passage.Should().NotBeNull();

        Play(view, 0.5);

        view.Marked.Passage.Should().BeNull();
        view.Marked.Words.Should().ContainSingle();
    }

    /// <summary>
    /// Correcting a word pauses the sound and puts the word into the field.
    /// </summary>
    [Fact]
    public void BeginCorrection_PausesTheSound_AndFillsTheField()
    {
        var view = Window();

        view.PointAt(10);
        view.CanCorrectHere.Should().BeTrue();
        view.CanRevertHere.Should().BeFalse();
        view.BeginCorrectionCommand.Execute(null);

        _audio.Received().Pause();
        view.IsEditing.Should().BeTrue();
        view.EditText.Should().Be("бюжет");
        view.EditRange.Should().Be(new TextMark(9, 5));
    }

    /// <summary>
    /// A saved correction changes the text, the export and the summary source, and is marked.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrection_ChangesTheText()
    {
        var view = Window();
        view.PointAt(10);
        view.BeginCorrectionCommand.Execute(null);

        view.EditText = "бюджет";
        await view.SaveCorrectionCommand.ExecuteAsync(null);

        view.IsEditing.Should().BeFalse();
        view.Transcript.Text.Should().StartWith("Обсудили бюджет.");
        view.Marked.Text.Should().StartWith("Обсудили бюджет.");
        view.Marked.Corrections.Should().Equal(new TextMark(9, 6));
        view.HintAt(10).Should().Be(TestTexts.Russian.Format("Record.WasText", "бюжет"));
        view.HintAt(2).Should().BeNull();
    }

    /// <summary>
    /// A correction taken back from the menu gives the recognized text back.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Revert_GivesTheRecognizedTextBack()
    {
        var view = Window(new RecordText(_transcript, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", DateTimeOffset.UnixEpoch)]));

        view.PointAt(10);
        view.CanRevertHere.Should().BeTrue();
        await view.RevertCommand.ExecuteAsync(null);

        view.Transcript.Text.Should().StartWith("Обсудили бюжет.");
        view.Marked.Corrections.Should().BeNullOrEmpty();
    }

    /// <summary>
    /// A click on a corrected word inside a wider selection offers to take that correction back.
    /// </summary>
    [Fact]
    public void PointAt_InASelection_FindsTheCorrectionUnderTheClick()
    {
        var view = Window(new RecordText(_transcript, [new TextCorrection(0, 9, 5, "бюжет", "бюджет", DateTimeOffset.UnixEpoch)]));

        view.PointAt(10, new TextMark(0, 16));

        view.CanRevertHere.Should().BeTrue();
        view.CanCorrectHere.Should().BeTrue();
    }

    /// <summary>
    /// A deleted word tells what it was.
    /// </summary>
    [Fact]
    public void HintAt_ADeletion_TellsWhatWasDeleted()
    {
        var view = Window(new RecordText(_transcript, [new TextCorrection(0, 8, 7, " бюжет.", string.Empty, DateTimeOffset.UnixEpoch)]));

        view.HintAt(7).Should().Be(TestTexts.Russian.Format("Record.WasDeleted", " бюжет."));
    }

    /// <summary>
    /// A phrase cannot be emptied: the field says so and the correction cannot be saved.
    /// </summary>
    [Fact]
    public void EmptyPhrase_CannotBeSaved()
    {
        var view = Window();
        view.PointAt(15 + _newLine, new TextMark(15 + _newLine, 10));
        view.BeginCorrectionCommand.Execute(null);
        view.EditText.Should().Be("Потом чай.");

        view.EditText = " ";

        view.CanSaveCorrection.Should().BeFalse();
        view.SaveCorrectionCommand.CanExecute(null).Should().BeFalse();
        view.EditHint.Should().Be(TestTexts.Russian.Get("Record.CannotEmpty"));
    }

    /// <summary>
    /// A correction the history could not keep is reported, and the text stays as it was.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task SaveCorrection_WhenTheHistoryFails_TellsWhy()
    {
        _history.SaveCorrectionsAsync(RECORD_ID, Arg.Any<IReadOnlyList<TextCorrection>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk"));
        var view = Window();
        view.PointAt(10);
        view.BeginCorrectionCommand.Execute(null);

        view.EditText = "бюджет";
        await view.SaveCorrectionCommand.ExecuteAsync(null);

        view.IsStatusError.Should().BeTrue();
        view.Status.Should().Be(TestTexts.Russian.Format("Record.CorrectFailed", "disk"));
        view.Transcript.Text.Should().StartWith("Обсудили бюжет.");
    }

    /// <summary>
    /// A text that is not in the history cannot be corrected.
    /// </summary>
    [Fact]
    public void WithoutCorrector_CannotCorrect()
    {
        var view = new RecordViewModel(TestTexts.Russian, _transcript, new MarkdownTranscriptWriter(TestTexts.Russian), Substitute.For<IDialogService>());

        view.PointAt(10);

        view.CanCorrect.Should().BeFalse();
        view.CanCorrectHere.Should().BeFalse();
    }

    /// <summary>
    /// Opens the window of the record with its player and its corrections.
    /// </summary>
    /// <param name="text">The record with its corrections; the record without any when omitted.</param>
    /// <param name="focus">The found passage the record is opened at, or <see langword="null"/>.</param>
    /// <returns>The window.</returns>
    private RecordViewModel Window(RecordText? text = null, RecordFocus? focus = null)
    {
        text ??= new RecordText(_transcript, []);
        var corrector = new RecordCorrector(_history, RECORD_ID, text, new FakeTimeProvider());

        return new RecordViewModel(
            TestTexts.Russian,
            corrector.Current.Transcript,
            new MarkdownTranscriptWriter(TestTexts.Russian),
            Substitute.For<IDialogService>(),
            new PlayerViewModel(TestTexts.Russian, _audio),
            focus: focus,
            corrector: corrector);
    }

    /// <summary>
    /// Lets the sound play at a moment and the window follow it.
    /// </summary>
    /// <param name="view">The window.</param>
    /// <param name="seconds">The moment of the sound, in seconds.</param>
    private void Play(RecordViewModel view, double seconds)
    {
        _audio.State.Returns(PlaybackState.Playing);
        _audio.Position.Returns(TimeSpan.FromSeconds(seconds));
        view.Player!.Refresh();
        view.FollowPlayer();
    }
}
