using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.History;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Search;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// The page of the history: every recognized text, the newest first; a record opens in its window and can be deleted
/// for good. The records are searched by words and meaning, and questions about them are answered by a model.
/// </summary>
public sealed partial class HistoryViewModel : PageViewModelBase
{
    /// <summary>
    /// The history of the recognitions.
    /// </summary>
    private readonly ITranscriptHistory _history;

    /// <summary>
    /// Opens the window of a record.
    /// </summary>
    private readonly IRecordViewer _viewer;

    /// <summary>
    /// Asks to confirm a deletion.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Whether the list is being read.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    /// <summary>
    /// What went wrong with the history, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(IsEmpty))]
    private string? _errorMessage;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="history">The history of the recognitions.</param>
    /// <param name="viewer">Opens the window of a record.</param>
    /// <param name="dialogs">Asks to confirm a deletion.</param>
    /// <param name="search">Searches the records and answers questions about them.</param>
    public HistoryViewModel(ILocalizer localizer, ITranscriptHistory history, IRecordViewer viewer, IDialogService dialogs, IRecordSearchService search)
        : base(localizer)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        Search = new HistorySearchViewModel(localizer, search, viewer);
        Entries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Gets the records, the newest first.
    /// </summary>
    public ObservableCollection<HistoryItemViewModel> Entries { get; } = [];

    /// <summary>
    /// Gets the search of the records.
    /// </summary>
    public HistorySearchViewModel Search { get; }

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Gets a value indicating whether the history is read and has no records.
    /// </summary>
    public bool IsEmpty => !IsLoading && !HasError && Entries.Count == 0;

    /// <inheritdoc />
    public override void Activate()
    {
        _ = LoadAsync();
    }

    /// <summary>
    /// Reads the list of the records again.
    /// </summary>
    /// <returns>A task that completes when the list is filled.</returns>
    public async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var entries = await _history.ListAsync(CancellationToken.None);

            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(new HistoryItemViewModel(Localizer, entry, OpenAsync, DeleteAsync));
            }
        }
        catch (IOException ex)
        {
            Entries.Clear();
            ErrorMessage = Localizer.Format("History.LoadFailed", Localizer.Describe(ex));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        foreach (var item in Entries)
        {
            item.Reword();
        }

        Search.Reword();
    }

    /// <summary>
    /// Opens a record in its window; a record deleted meanwhile leaves the list.
    /// </summary>
    /// <param name="item">The record.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    private async Task OpenAsync(HistoryItemViewModel item)
    {
        ErrorMessage = null;
        try
        {
            if (!await _viewer.OpenAsync(item.Entry.Id, CancellationToken.None))
            {
                Entries.Remove(item);
                ErrorMessage = Localizer.Get("History.Missing");
            }
        }
        catch (IOException ex)
        {
            ErrorMessage = Localizer.Format("History.OpenFailed", Localizer.Describe(ex));
        }
    }

    /// <summary>
    /// Deletes a record for good once the user confirms it.
    /// </summary>
    /// <param name="item">The record.</param>
    /// <returns>A task that completes when the record is deleted or kept.</returns>
    private async Task DeleteAsync(HistoryItemViewModel item)
    {
        var question = new ConfirmDialogViewModel(
            Localizer.Get("History.DeleteTitle"),
            Localizer.Format("History.DeleteMessage", item.SourceName),
            Localizer.Get("History.DeleteConfirm"));
        if (!await _dialogs.ConfirmAsync(question))
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            await _history.DeleteAsync(item.Entry.Id, CancellationToken.None);
            Entries.Remove(item);
        }
        catch (IOException ex)
        {
            ErrorMessage = Localizer.Format("History.DeleteFailed", Localizer.Describe(ex));
        }
    }

    /// <summary>
    /// Hides the failure.
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
    }
}
