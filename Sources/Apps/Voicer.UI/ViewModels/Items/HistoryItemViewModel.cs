using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Enums;
using Voicer.Core.History;
using Voicer.Localization;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A record in the list of the history: whether it was recognized or generated, the file, when and how it was made, the
/// beginning of its text, and the buttons that open and delete it.
/// </summary>
/// <remarks>
/// The items are made anew whenever the list is read, so they do not follow the language themselves: the page words
/// them again (<see cref="Reword"/>).
/// </remarks>
public sealed partial class HistoryItemViewModel : ObservableObject
{
    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Opens the record.
    /// </summary>
    private readonly Func<HistoryItemViewModel, Task> _open;

    /// <summary>
    /// Deletes the record.
    /// </summary>
    private readonly Func<HistoryItemViewModel, Task> _delete;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryItemViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="entry">The record.</param>
    /// <param name="open">Opens the record.</param>
    /// <param name="delete">Deletes the record.</param>
    public HistoryItemViewModel(ILocalizer localizer, HistoryEntry entry, Func<HistoryItemViewModel, Task> open, Func<HistoryItemViewModel, Task> delete)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _delete = delete ?? throw new ArgumentNullException(nameof(delete));
    }

    /// <summary>
    /// Gets the record.
    /// </summary>
    public HistoryEntry Entry { get; }

    /// <summary>
    /// Gets the name of the recognized file.
    /// </summary>
    public string SourceName => Entry.SourceName;

    /// <summary>
    /// Gets when the file was recognized, how long its speech lasts, by which model and in which language.
    /// </summary>
    public string Details => _localizer.Format(
        "History.Details",
        DisplayText.Moment(_localizer, Entry.CreatedAt),
        DisplayText.Duration(Entry.Duration),
        Entry.ModelName,
        DisplayText.Language(_localizer, Entry.Language));

    /// <summary>
    /// Gets a value indicating whether the record is a generated speech, not a recognized one.
    /// </summary>
    public bool IsGeneration => Entry.Kind == RecordKind.Generation;

    /// <summary>
    /// Gets how the record was made, in words: a recognition or a generation.
    /// </summary>
    public string KindText => _localizer.Get(IsGeneration ? "History.Kind.Generation" : "History.Kind.Transcription");

    /// <summary>
    /// Gets the icon of how the record was made.
    /// </summary>
    public string KindIcon => IsGeneration ? "Icon.Speaker" : "Icon.Mic";

    /// <summary>
    /// Gets a value indicating whether the sound of the file is kept and can be listened to.
    /// </summary>
    public bool HasAudio => Entry.HasAudio;

    /// <summary>
    /// Gets the beginning of the text, or a note that no speech was recognized.
    /// </summary>
    public string Preview => Entry.Preview.Length == 0 ? _localizer.Get("Result.Empty") : Entry.Preview;

    /// <summary>
    /// Words the texts again in the language now chosen.
    /// </summary>
    public void Reword()
    {
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Opens the record.
    /// </summary>
    /// <returns>A task that completes when the window of the record is closed.</returns>
    [RelayCommand]
    private Task OpenAsync()
    {
        return _open(this);
    }

    /// <summary>
    /// Deletes the record after the user confirms it.
    /// </summary>
    /// <returns>A task that completes when the record is deleted or kept.</returns>
    [RelayCommand]
    private Task DeleteAsync()
    {
        return _delete(this);
    }
}
