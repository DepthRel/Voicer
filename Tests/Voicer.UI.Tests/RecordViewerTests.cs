using FluentAssertions;
using NSubstitute;
using Voicer.Core.History;
using Voicer.Core.Playback;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Summaries;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the opening of a record: the text from the history, its sound in a temporary file for the player, and
/// everything released when the window closes.
/// </summary>
public sealed class RecordViewerTests : IDisposable
{
    /// <summary>
    /// The text of the record.
    /// </summary>
    private static readonly Transcript _transcript = new("talk.mp4", "Whisper Small Q5", "CPU", "en", DateTimeOffset.UnixEpoch, []);

    /// <summary>
    /// The history.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// The dialogs.
    /// </summary>
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    /// <summary>
    /// The player made for the sound.
    /// </summary>
    private readonly IAudioPlayer _player = Substitute.For<IAudioPlayer>();

    /// <summary>
    /// A temporary folder of the sound.
    /// </summary>
    private readonly TemporaryAudioFolder _temporary = new(Path.Combine(Path.GetTempPath(), "voicer-viewer-" + Guid.NewGuid().ToString("N")));

    /// <summary>
    /// The files the players were made for.
    /// </summary>
    private readonly List<string> _playedFiles = [];

    /// <summary>
    /// The window shown last.
    /// </summary>
    private RecordViewModel? _shown;

    /// <summary>
    /// The viewer under test.
    /// </summary>
    private readonly RecordViewer _viewer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordViewerTests"/> class.
    /// </summary>
    public RecordViewerTests()
    {
        _history.FindAsync(3, Arg.Any<CancellationToken>()).Returns(_transcript);
        _player.Duration.Returns(TimeSpan.FromMinutes(2));
        _player.Speed.Returns(1);
        _dialogs.ShowRecordAsync(Arg.Do<RecordViewModel>(r => _shown = r)).Returns(Task.CompletedTask);
        _viewer = new RecordViewer(TestTexts.Russian, _history, _dialogs, _temporary, (file, _) =>
        {
            _playedFiles.Add(file);

            return _player;
        }, Substitute.For<IRecordSummaryService>());
    }

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_temporary.Directory))
        {
            Directory.Delete(_temporary.Directory, recursive: true);
        }
    }

    /// <summary>
    /// A record with a sound opens with a player of a temporary copy; closing the window releases both.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_WithASound_GivesThePlayerAndCleansUp()
    {
        _history.ExportAudioAsync(3, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMinutes(2))
            .AndDoes(call => File.WriteAllBytes(call.ArgAt<string>(1), [1, 2, 3]));

        (await _viewer.OpenAsync(3, CancellationToken.None)).Should().BeTrue();

        _shown!.HasPlayer.Should().BeTrue();
        _shown.HasAudioNote.Should().BeFalse();
        _playedFiles.Should().ContainSingle().Which.Should().StartWith(_temporary.Directory);
        _player.Received(1).Dispose();
        File.Exists(_playedFiles[0]).Should().BeFalse();
    }

    /// <summary>
    /// A record opened at a found passage plays from its start and shows it marked with the words of the query.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_AtAPassage_StartsThePlayerThereAndMarksIt()
    {
        var spoken = _transcript with
        {
            Segments =
            [
                new(TimeSpan.Zero, TimeSpan.FromSeconds(40), "Hello."),
                new(TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(50), "The budget is approved."),
            ],
        };
        _history.FindAsync(3, Arg.Any<CancellationToken>()).Returns(spoken);
        _history.ExportAudioAsync(3, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(TimeSpan.FromMinutes(2));

        await _viewer.OpenAsync(3, CancellationToken.None, new RecordFocus(TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(50), "budgets"));

        _player.Received(1).Seek(TimeSpan.FromSeconds(42));
        var passageStart = "Hello.".Length + Environment.NewLine.Length;
        _shown!.Marked.Passage.Should().Be(new TextMark(passageStart, "The budget is approved.".Length));
        _shown.Marked.Words.Should().Equal(new TextMark(passageStart + 4, 6));
    }

    /// <summary>
    /// A record opened from the list, not from the search, marks nothing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_FromTheList_MarksNothing()
    {
        await _viewer.OpenAsync(3, CancellationToken.None);

        _shown!.Marked.HasMarks.Should().BeFalse();
        _shown.Marked.Text.Should().Be(_shown.Text);
    }

    /// <summary>
    /// A record without a sound opens with a note instead of a player.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_WithoutASound_SaysSo()
    {
        _history.ExportAudioAsync(3, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((TimeSpan?)null);

        await _viewer.OpenAsync(3, CancellationToken.None);

        _shown!.HasPlayer.Should().BeFalse();
        _shown.AudioNote.Should().Be(TestTexts.Russian.Get("Player.NoAudio"));
        _playedFiles.Should().BeEmpty();
    }

    /// <summary>
    /// A sound that cannot be read still lets the text be read, with the reason.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_WhenTheSoundFails_ShowsTheText()
    {
        _history.ExportAudioAsync(3, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<TimeSpan?>(new IOException("locked")));

        await _viewer.OpenAsync(3, CancellationToken.None);

        _shown!.HasPlayer.Should().BeFalse();
        _shown.AudioNote.Should().Be(TestTexts.Russian.Format("Player.LoadFailed", "locked"));
    }

    /// <summary>
    /// A record that is gone is not shown.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_OfAMissingRecord_ReturnsFalse()
    {
        (await _viewer.OpenAsync(99, CancellationToken.None)).Should().BeFalse();

        await _dialogs.DidNotReceiveWithAnyArgs().ShowRecordAsync(default!);
    }
}
