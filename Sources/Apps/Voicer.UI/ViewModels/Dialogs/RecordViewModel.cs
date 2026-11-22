using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Corrections;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// The window of a record of the history: what was recognized, how and when, the player of its sound with the phrase
/// being said marked in the text, the corrections of the text, the summary, and its export to Markdown.
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
    /// The corrections of the record, or <see langword="null"/> when its text cannot be corrected.
    /// </summary>
    private readonly RecordCorrector? _corrector;

    /// <summary>
    /// The found passage the record was opened at, or <see langword="null"/>.
    /// </summary>
    private readonly RecordFocus? _focus;

    /// <summary>
    /// The text of the record when it cannot be corrected.
    /// </summary>
    private readonly Transcript _transcript;

    /// <summary>
    /// Whether the sound has been played since the window opened: the found passage gives way to the phrase being said.
    /// </summary>
    private bool _played;

    /// <summary>
    /// The index of the phrase being said, or <see langword="null"/>.
    /// </summary>
    private int? _phraseSegment;

    /// <summary>
    /// The piece the menu of the text points at, or <see langword="null"/>.
    /// </summary>
    private CorrectionTarget? _menuTarget;

    /// <summary>
    /// The corrected place the menu of the text points at, or <see langword="null"/>.
    /// </summary>
    private CorrectionMark? _menuMark;

    /// <summary>
    /// The piece being corrected in the field, or <see langword="null"/>.
    /// </summary>
    private CorrectionTarget? _editTarget;

    /// <summary>
    /// What happened to the last export or correction, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    /// <summary>
    /// Whether the last export or correction failed.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>
    /// The recognized text with what is marked in it.
    /// </summary>
    [ObservableProperty]
    private MarkedText _marked;

    /// <summary>
    /// Where the phrase being said lies in the text, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    private TextMark? _phrase;

    /// <summary>
    /// Whether the menu of the text points at a piece that can be corrected.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BeginCorrectionCommand))]
    private bool _canCorrectHere;

    /// <summary>
    /// Whether the menu of the text points at a corrected place.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private bool _canRevertHere;

    /// <summary>
    /// Whether the field of a correction is open.
    /// </summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// What the field of a correction holds.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveCorrection), nameof(EditHint), nameof(HasEditHint))]
    [NotifyCanExecuteChangedFor(nameof(SaveCorrectionCommand))]
    private string _editText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="transcript">The transcript, with its corrections applied.</param>
    /// <param name="writer">The writer of the Markdown document.</param>
    /// <param name="dialogs">The dialogs that pick where to save.</param>
    /// <param name="player">The player of the kept sound, or <see langword="null"/> when there is none.</param>
    /// <param name="audioNote">Why there is no player, or <see langword="null"/>.</param>
    /// <param name="summary">The summary of the record, or <see langword="null"/> for a text that is not in the history.</param>
    /// <param name="focus">The found passage the record is opened at, or <see langword="null"/>.</param>
    /// <param name="corrector">The corrections of the record, or <see langword="null"/> when its text cannot be corrected.</param>
    public RecordViewModel(
        ILocalizer localizer,
        Transcript transcript,
        MarkdownTranscriptWriter writer,
        IDialogService dialogs,
        PlayerViewModel? player = null,
        string? audioNote = null,
        SummaryPanelViewModel? summary = null,
        RecordFocus? focus = null,
        RecordCorrector? corrector = null)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        Details = localizer.Format(
            "Result.Details",
            DisplayText.Moment(localizer, transcript.CreatedAt),
            DisplayText.Duration(player is null ? transcript.Duration : TimeSpan.FromSeconds(player.DurationSeconds)),
            transcript.ModelName,
            transcript.DeviceName,
            DisplayText.Language(localizer, transcript.Language));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _corrector = corrector;
        _focus = focus;
        Title = localizer.Format("Result.Title", transcript.SourceName);
        Player = player;
        AudioNote = player is null ? audioNote ?? localizer.Get("Player.NoAudio") : null;
        Summary = summary;
        _marked = Mark();
    }

    /// <summary>
    /// Gets the transcript with its corrections applied.
    /// </summary>
    public Transcript Transcript => _corrector?.Current.Transcript ?? _transcript;

    /// <summary>
    /// Gets the title of the window.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets when, by which model and on which device the text was recognized, how long the record lasts and its language.
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Gets the recognized text, with its corrections.
    /// </summary>
    public string Text => Transcript.Segments.Count == 0 ? _localizer.Get("Result.Empty") : Transcript.Text;

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
    /// Gets a value indicating whether the text of the record can be corrected: a recognition kept in the history.
    /// </summary>
    public bool CanCorrect => _corrector is not null;

    /// <summary>
    /// Gets where the piece being corrected lies in the text, or <see langword="null"/>.
    /// </summary>
    public TextMark? EditRange => _editTarget?.Shown;

    /// <summary>
    /// Gets a value indicating whether the field holds a correction that can be saved.
    /// </summary>
    public bool CanSaveCorrection => _editTarget is not null && _corrector is not null && _corrector.CanPut(_editTarget, EditText);

    /// <summary>
    /// Gets why the correction in the field cannot be saved, or <see langword="null"/>.
    /// </summary>
    public string? EditHint => _editTarget is not null && !CanSaveCorrection ? _localizer.Get("Record.CannotEmpty") : null;

    /// <summary>
    /// Gets a value indicating whether the field tells why it cannot be saved.
    /// </summary>
    public bool HasEditHint => EditHint is not null;

    /// <summary>
    /// Follows the player: once the sound has played, marks the phrase being said in place of the found passage. The
    /// window calls it after every refresh of the player.
    /// </summary>
    public void FollowPlayer()
    {
        if (Player is null)
        {
            return;
        }

        if (Player.IsPlaying)
        {
            _played = true;
        }

        if (!_played)
        {
            return;
        }

        var segment = TranscriptLayout.SegmentAt(Transcript, TimeSpan.FromSeconds(Player.PositionSeconds));
        if (segment == _phraseSegment && Marked.Passage is null)
        {
            return;
        }

        _phraseSegment = segment;
        Remark();
    }

    /// <summary>
    /// Points the menu of the text at a click or a selection: what it would correct and which corrected place is there.
    /// </summary>
    /// <param name="at">The clicked character of the text; the beginning of the selection for a menu from the keyboard.</param>
    /// <param name="selection">The selection the click is in, or <see langword="null"/> for a click on a word.</param>
    public void PointAt(int at, TextMark? selection = null)
    {
        _menuTarget = _corrector?.Target(at, selection);
        _menuMark = _corrector?.MarkAt(at);
        CanCorrectHere = _menuTarget is not null;
        CanRevertHere = _menuMark is not null;
    }

    /// <summary>
    /// Tells what was recognized at a corrected place of the text.
    /// </summary>
    /// <param name="index">The character, from the beginning of the text.</param>
    /// <returns>The hint, or <see langword="null"/> when the character was not corrected.</returns>
    public string? HintAt(int index)
    {
        if (_corrector?.MarkAt(index) is not { } mark)
        {
            return null;
        }

        var correction = mark.Correction;

        return _localizer.Format(correction.Replacement.Length == 0 ? "Record.WasDeleted" : "Record.WasText", correction.Original);
    }

    /// <summary>
    /// Opens the field of a correction for the piece the menu points at, with the sound paused.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCorrectHere))]
    private void BeginCorrection()
    {
        if (_menuTarget is null)
        {
            return;
        }

        Player?.Pause();
        _editTarget = _menuTarget;
        OnPropertyChanged(nameof(EditRange));
        EditText = _menuTarget.ShownText;
        IsEditing = true;
    }

    /// <summary>
    /// Keeps the correction in the field and shows it; a failure is reported and the text stays.
    /// </summary>
    /// <returns>A task that completes when the correction is kept or failed.</returns>
    [RelayCommand(CanExecute = nameof(CanSaveCorrection))]
    private async Task SaveCorrectionAsync()
    {
        if (_editTarget is not { } target || _corrector is null)
        {
            return;
        }

        IsEditing = false;
        await ChangeTextAsync(() => _corrector.PutAsync(target, EditText, CancellationToken.None));
    }

    /// <summary>
    /// Closes the field of a correction without keeping it.
    /// </summary>
    [RelayCommand]
    private void CancelCorrection()
    {
        IsEditing = false;
    }

    /// <summary>
    /// Takes back the correction the menu points at.
    /// </summary>
    /// <returns>A task that completes when the change is kept or failed.</returns>
    [RelayCommand(CanExecute = nameof(CanRevertHere))]
    private async Task RevertAsync()
    {
        if (_menuMark is not { } mark || _corrector is null)
        {
            return;
        }

        await ChangeTextAsync(() => _corrector.RevertAsync(mark, CancellationToken.None));
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

    /// <summary>
    /// Changes the corrections of the text, then shows the new text everywhere it is used; a failure is reported and the
    /// text stays.
    /// </summary>
    /// <param name="change">The change, kept in the history.</param>
    /// <returns>A task that completes when the change is kept or failed.</returns>
    private async Task ChangeTextAsync(Func<Task> change)
    {
        try
        {
            await change();
        }
        catch (IOException ex)
        {
            IsStatusError = true;
            Status = _localizer.Format("Record.CorrectFailed", _localizer.Describe(ex));

            return;
        }

        Status = null;
        _menuTarget = null;
        _menuMark = null;
        CanCorrectHere = false;
        CanRevertHere = false;
        Summary?.UseText(Transcript);
        OnPropertyChanged(nameof(Transcript));
        OnPropertyChanged(nameof(Text));
        Remark();
    }

    /// <summary>
    /// Marks the text again: the phrase being said, the corrected places, and the found passage until the sound plays.
    /// </summary>
    private void Remark()
    {
        Marked = Mark();
        Phrase = Marked.Phrase;
    }

    /// <summary>
    /// Marks the text: the found passage and the words of the query in it until the sound plays, the phrase being said
    /// after, and the corrected places.
    /// </summary>
    /// <returns>The text with its marks.</returns>
    private MarkedText Mark()
    {
        var transcript = Transcript;
        var text = Text;
        if (transcript.Segments.Count == 0)
        {
            return new MarkedText(text, []);
        }

        var ranges = TranscriptLayout.Ranges(transcript);
        var phrase = _phraseSegment is { } segment ? ranges[segment] : null;
        var corrections = _corrector?.Current.Marks.Select(m => m.Range).ToList();

        IReadOnlyList<TextMark> words = [];
        TextMark? passage = null;
        if (_focus is not null && SearchMarks.Passage(transcript, _focus.Start, _focus.End) is { } found)
        {
            words = [.. SearchMarks.Words(text, FullTextQuery.Prefixes(_focus.Query)).Words
                .Where(word => word.Start >= found.Start && word.End <= found.End)];
            passage = _played ? null : found;
        }

        return new MarkedText(text, words, passage, phrase, corrections);
    }
}
