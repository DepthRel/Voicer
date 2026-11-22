using FluentAssertions;
using NSubstitute;
using Voicer.Core.History;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Search;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the page of the history: the list, opening a record and deleting it for good.
/// </summary>
public sealed class HistoryViewModelTests
{
    /// <summary>
    /// A record recognized in Russian.
    /// </summary>
    private static readonly HistoryEntry _meeting = new(
        2,
        "meeting.mp4",
        "Whisper Large v3 Turbo Q8",
        "RTX 4070",
        "ru",
        new DateTimeOffset(2026, 11, 18, 14, 30, 0, TimeSpan.FromHours(3)),
        TimeSpan.FromMinutes(65),
        "Добрый день, коллеги.",
        true);

    /// <summary>
    /// A record of a file without speech.
    /// </summary>
    private static readonly HistoryEntry _silence = new(1, "silence.wav", "Whisper Small Q5", "CPU", null, DateTimeOffset.UnixEpoch, TimeSpan.Zero, string.Empty, false);

    /// <summary>
    /// The history.
    /// </summary>
    private readonly ITranscriptHistory _history = Substitute.For<ITranscriptHistory>();

    /// <summary>
    /// Opens the window of a record.
    /// </summary>
    private readonly IRecordViewer _viewer = Substitute.For<IRecordViewer>();

    /// <summary>
    /// The dialogs that ask to confirm a deletion.
    /// </summary>
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();

    /// <summary>
    /// The page under test, in Russian.
    /// </summary>
    private readonly HistoryViewModel _page;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryViewModelTests"/> class.
    /// </summary>
    public HistoryViewModelTests()
    {
        _history.ListAsync(default).ReturnsForAnyArgs([_meeting, _silence]);
        _page = new HistoryViewModel(TestTexts.Russian, _history, _viewer, _dialogs, Substitute.For<IRecordSearchService>());
    }

    /// <summary>
    /// The list shows every record as the history gives them, worded in the language of the user.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Load_ListsTheRecords()
    {
        await _page.LoadAsync();

        _page.Entries.Select(e => e.SourceName).Should().Equal("meeting.mp4", "silence.wav");
        _page.IsEmpty.Should().BeFalse();
        _page.Entries[0].Details.Should().Be(TestTexts.Russian.Format(
            "History.Details",
            _meeting.CreatedAt.ToString("g", TestTexts.Russian.Culture),
            "1:05:00",
            "Whisper Large v3 Turbo Q8",
            TestTexts.Russian.Get("SpeechLanguage.ru")));
        _page.Entries[0].Preview.Should().Be("Добрый день, коллеги.");
        _page.Entries[1].Preview.Should().Be(TestTexts.Russian.Get("Result.Empty"));
    }

    /// <summary>
    /// An empty history says so.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Load_OfAnEmptyHistory_IsEmpty()
    {
        _history.ListAsync(default).ReturnsForAnyArgs([]);

        await _page.LoadAsync();

        _page.IsEmpty.Should().BeTrue();
    }

    /// <summary>
    /// A history that cannot be read shows the failure instead of an empty list.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Load_ShowsAFailure()
    {
        _history.ListAsync(default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<HistoryEntry>>(new IOException("locked")));

        await _page.LoadAsync();

        _page.ErrorMessage.Should().Be(TestTexts.Russian.Format("History.LoadFailed", "locked"));
        _page.IsEmpty.Should().BeFalse();
    }

    /// <summary>
    /// Opening a record shows its window by its identifier.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_ShowsTheRecord()
    {
        _viewer.OpenAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        await _page.LoadAsync();

        await _page.Entries[0].OpenCommand.ExecuteAsync(null);

        await _viewer.Received(1).OpenAsync(2, Arg.Any<CancellationToken>());
        _page.HasError.Should().BeFalse();
    }

    /// <summary>
    /// A record corrected in its window shows its corrected beginning in the list when the window closes; the other
    /// records stay as they were.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_ThenCorrected_ShowsTheNewBeginning()
    {
        _viewer.OpenAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        await _page.LoadAsync();
        var other = _page.Entries[1];
        _history.ListAsync(default).ReturnsForAnyArgs([_meeting with { Preview = "Добрый вечер, коллеги." }, _silence]);

        await _page.Entries[0].OpenCommand.ExecuteAsync(null);

        _page.Entries[0].Preview.Should().Be("Добрый вечер, коллеги.");
        _page.Entries[1].Should().BeSameAs(other);
    }

    /// <summary>
    /// A record that is no longer in the history leaves the list with a note.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_OfAMissingRecord_RemovesIt()
    {
        _viewer.OpenAsync(2, Arg.Any<CancellationToken>()).Returns(false);
        await _page.LoadAsync();

        await _page.Entries[0].OpenCommand.ExecuteAsync(null);

        _page.Entries.Select(e => e.SourceName).Should().Equal("silence.wav");
        _page.ErrorMessage.Should().Be(TestTexts.Russian.Get("History.Missing"));
    }

    /// <summary>
    /// A confirmed deletion deletes the record from the history and the list; the question names the file.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_WhenConfirmed_DeletesForGood()
    {
        _dialogs.ConfirmAsync(Arg.Any<ConfirmDialogViewModel>()).Returns(true);
        _history.DeleteAsync(2, Arg.Any<CancellationToken>()).Returns(true);
        await _page.LoadAsync();

        await _page.Entries[0].DeleteCommand.ExecuteAsync(null);

        await _dialogs.Received(1).ConfirmAsync(Arg.Is<ConfirmDialogViewModel>(q => q.Message == TestTexts.Russian.Format("History.DeleteMessage", "meeting.mp4")));
        await _history.Received(1).DeleteAsync(2, Arg.Any<CancellationToken>());
        _page.Entries.Select(e => e.SourceName).Should().Equal("silence.wav");
    }

    /// <summary>
    /// A deletion the user did not confirm changes nothing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_WhenCancelled_KeepsTheRecord()
    {
        _dialogs.ConfirmAsync(Arg.Any<ConfirmDialogViewModel>()).Returns(false);
        await _page.LoadAsync();

        await _page.Entries[0].DeleteCommand.ExecuteAsync(null);

        await _history.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
        _page.Entries.Should().HaveCount(2);
    }

    /// <summary>
    /// A deletion the history refused keeps the record in the list and shows why.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Delete_ThatFailed_KeepsTheRecord()
    {
        _dialogs.ConfirmAsync(Arg.Any<ConfirmDialogViewModel>()).Returns(true);
        _history.DeleteAsync(2, Arg.Any<CancellationToken>()).Returns(Task.FromException<bool>(new IOException("read-only")));
        await _page.LoadAsync();

        await _page.Entries[0].DeleteCommand.ExecuteAsync(null);

        _page.Entries.Should().HaveCount(2);
        _page.ErrorMessage.Should().Be(TestTexts.Russian.Format("History.DeleteFailed", "read-only"));
    }
}
