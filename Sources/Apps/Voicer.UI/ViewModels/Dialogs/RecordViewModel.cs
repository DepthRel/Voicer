using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// The window of a record of the history: what was recognized, how and when, the player of its sound, the text read from
/// the history, and its export to Markdown.
/// </summary>
/// <remarks>
/// The window is modal, so the language cannot change while it is open: the texts are worded once.
/// </remarks>
public sealed partial class RecordViewModel : ObservableObject
{
    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// The writer of the Markdown document.
    /// </summary>
    private readonly MarkdownTranscriptWriter _writer;

    /// <summary>
    /// The dialogs that pick where to save.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// What happened to the last export, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    /// <summary>
    /// Whether the last export failed.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="transcript">The transcript.</param>
    /// <param name="writer">The writer of the Markdown document.</param>
    /// <param name="dialogs">The dialogs that pick where to save.</param>
    /// <param name="player">The player of the kept sound, or <see langword="null"/> when there is none.</param>
    /// <param name="audioNote">Why there is no player, or <see langword="null"/>.</param>
    /// <param name="summary">The summary of the record, or <see langword="null"/> for a text that is not in the history.</param>
    /// <param name="focus">The found passage the record is opened at, or <see langword="null"/>.</param>
    public RecordViewModel(
        ILocalizer localizer,
        Transcript transcript,
        MarkdownTranscriptWriter writer,
        IDialogService dialogs,
        PlayerViewModel? player = null,
        string? audioNote = null,
        SummaryPanelViewModel? summary = null,
        RecordFocus? focus = null)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        Transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        Details = localizer.Format(
            "Result.Details",
            DisplayText.Moment(localizer, transcript.CreatedAt),
            DisplayText.Duration(player is null ? transcript.Duration : TimeSpan.FromSeconds(player.DurationSeconds)),
            transcript.ModelName,
            transcript.DeviceName,
            DisplayText.Language(localizer, transcript.Language));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Title = localizer.Format("Result.Title", transcript.SourceName);
        Text = transcript.Segments.Count == 0 ? localizer.Get("Result.Empty") : transcript.Text;
        Player = player;
        AudioNote = player is null ? audioNote ?? localizer.Get("Player.NoAudio") : null;
        Summary = summary;
        Marked = Mark(transcript, Text, focus);
    }

    /// <summary>
    /// Gets the transcript.
    /// </summary>
    public Transcript Transcript { get; }

    /// <summary>
    /// Gets the title of the window.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets when, by which model and on which device the text was recognized, how long the record lasts and its language.
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Gets the recognized text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the recognized text with the found passage and the words of the query in it marked; nothing is marked when
    /// the record was not opened from the search.
    /// </summary>
    public MarkedText Marked { get; }

    /// <summary>
    /// Gets the player of the kept sound, or <see langword="null"/> when the record has none.
    /// </summary>
    public PlayerViewModel? Player { get; }

    /// <summary>
    /// Gets a value indicating whether the record has a player.
    /// </summary>
    public bool HasPlayer => Player is not null;

    /// <summary>
    /// Gets why the record cannot be listened to, or <see langword="null"/> when it can.
    /// </summary>
    public string? AudioNote { get; }

    /// <summary>
    /// Gets a value indicating whether the note about the missing sound is shown.
    /// </summary>
    public bool HasAudioNote => AudioNote is not null;

    /// <summary>
    /// Gets the summary of the record, or <see langword="null"/> for a text that is not in the history.
    /// </summary>
    public SummaryPanelViewModel? Summary { get; }

    /// <summary>
    /// Gets a value indicating whether the record has a summary area.
    /// </summary>
    public bool HasSummary => Summary is not null;

    /// <summary>
    /// Gets a value indicating whether a status of the export is shown.
    /// </summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>
    /// Marks the found passage in the text of the record and the words of the query inside it.
    /// </summary>
    /// <param name="transcript">The record.</param>
    /// <param name="text">The text shown.</param>
    /// <param name="focus">The found passage, or <see langword="null"/>.</param>
    /// <returns>The text with its marks.</returns>
    private static MarkedText Mark(Transcript transcript, string text, RecordFocus? focus)
    {
        if (focus is null || transcript.Segments.Count == 0 ||
            SearchMarks.Passage(transcript, focus.Start, focus.End) is not { } passage)
        {
            return new MarkedText(text, []);
        }

        var words = SearchMarks.Words(text, FullTextQuery.Prefixes(focus.Query)).Words
            .Where(word => word.Start >= passage.Start && word.End <= passage.End)
            .ToList();

        return new MarkedText(text, words, passage);
    }

    /// <summary>
    /// Saves the text, with its kept summary, as a Markdown file the user picks.
    /// </summary>
    /// <returns>A task that completes when the file is written or the user changed their mind.</returns>
    [RelayCommand]
    private async Task ExportAsync()
    {
        var suggested = Path.GetFileNameWithoutExtension(Transcript.SourcePath) + "." + MarkdownTranscriptWriter.EXTENSION;
        var path = await _dialogs.PickSaveFileAsync(
            _localizer.Get("Export.PickerTitle"),
            suggested,
            _localizer.Get("Export.FileType"),
            MarkdownTranscriptWriter.EXTENSION);
        if (path is null)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, _writer.Write(Transcript, Summary?.KeptText), new UTF8Encoding(false));
            IsStatusError = false;
            Status = _localizer.Format("Export.Saved", path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            IsStatusError = true;
            Status = _localizer.Format("Export.Failed", ex.Message);
        }
    }
}
