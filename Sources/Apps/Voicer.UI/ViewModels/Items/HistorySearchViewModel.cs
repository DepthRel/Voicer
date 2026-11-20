using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Search;
using Voicer.Localization;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Search;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// The search of the page of the history: the query, the passages found by words and meaning, or the answer of the
/// model with its sources; a passage opens its record at its moment.
/// </summary>
public sealed partial class HistorySearchViewModel : ObservableObject
{
    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Searches and answers.
    /// </summary>
    private readonly IRecordSearchService _search;

    /// <summary>
    /// Opens the record of a passage.
    /// </summary>
    private readonly IRecordViewer _viewer;

    /// <summary>
    /// Stops the work in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _work;

    /// <summary>
    /// What the user typed.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(AskCommand))]
    private string _query = string.Empty;

    /// <summary>
    /// Whether a search or an answer is in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand), nameof(AskCommand), nameof(CancelCommand))]
    private bool _isWorking;

    /// <summary>
    /// Whether the results are shown instead of the list of the records.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// What the search is doing or what it found, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string? _status;

    /// <summary>
    /// Why the search failed, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>
    /// The answer of the model, or <see langword="null"/> for a plain search.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnswer))]
    private string? _answer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistorySearchViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="search">Searches and answers.</param>
    /// <param name="viewer">Opens the record of a passage.</param>
    public HistorySearchViewModel(ILocalizer localizer, IRecordSearchService search, IRecordViewer viewer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
    }

    /// <summary>
    /// Gets the passages found, or the sources of the answer.
    /// </summary>
    public ObservableCollection<SearchHitViewModel> Hits { get; } = [];

    /// <summary>
    /// Gets a value indicating whether a status is shown.
    /// </summary>
    public bool HasStatus => !string.IsNullOrEmpty(Status);

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets a value indicating whether an answer is shown.
    /// </summary>
    public bool HasAnswer => Answer is not null;

    /// <summary>
    /// Words the results again in the language now chosen.
    /// </summary>
    public void Reword()
    {
        foreach (var hit in Hits)
        {
            hit.Reword();
        }
    }

    /// <summary>
    /// Finds the passages that best answer the query.
    /// </summary>
    /// <returns>A task that completes when the passages are shown, or the search failed or was stopped.</returns>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task SearchAsync()
    {
        var query = Query.Trim();
        var token = Begin(_localizer.Get("Search.Searching"), answer: false);
        try
        {
            var hits = await _search.SearchAsync(query, Indexing(), token);
            Show(hits, query, numbered: false);
            Status = hits.Count == 0
                ? _localizer.Get("Search.NothingFound")
                : _localizer.Format(_search.SearchesByMeaning ? "Search.Found" : "Search.FoundByWords", hits.Count);
        }
        catch (OperationCanceledException)
        {
            Status = null;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or InvalidOperationException or Win32Exception)
        {
            Fail(ex);
        }
        finally
        {
            End();
        }
    }

    /// <summary>
    /// Asks the model the query as a question and shows its answer with the passages it is based on.
    /// </summary>
    /// <returns>A task that completes when the answer is shown, or it failed or was stopped.</returns>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task AskAsync()
    {
        if (!_search.CanAsk)
        {
            IsActive = true;
            Error = _localizer.Get("Summary.NotConfigured");

            return;
        }

        var query = Query.Trim();
        var token = Begin(_localizer.Get("Search.Asking"), answer: true);
        var arriving = new Progress<string>(text =>
        {
            if (IsWorking)
            {
                Status = _localizer.Get("Search.Asking");
                Answer = text;
            }
        });

        try
        {
            var result = await _search.AskAsync(query, Indexing(), arriving, token);
            Answer = result.Answer;
            Show(result.Sources, query, numbered: true);
            Status = null;
        }
        catch (OperationCanceledException)
        {
            Status = null;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or InvalidOperationException or Win32Exception)
        {
            Fail(ex);
        }
        finally
        {
            End();
        }
    }

    /// <summary>
    /// Tells whether a search can start now.
    /// </summary>
    /// <returns><see langword="true"/> when something is typed and nothing runs.</returns>
    private bool CanStart()
    {
        return !IsWorking && !string.IsNullOrWhiteSpace(Query);
    }

    /// <summary>
    /// Stops the search or the answer.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsWorking))]
    private void Cancel()
    {
        _work?.Cancel();
    }

    /// <summary>
    /// Goes back to the list of the records.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        _work?.Cancel();
        IsActive = false;
        Hits.Clear();
        Answer = null;
        Status = null;
        Error = null;
    }

    /// <summary>
    /// Starts a search or an answer: forgets the last results and shows what is being done.
    /// </summary>
    /// <param name="status">What is being done.</param>
    /// <param name="answer">Whether an answer is expected.</param>
    /// <returns>The token that stops the work.</returns>
    private CancellationToken Begin(string status, bool answer)
    {
        Hits.Clear();
        Error = null;
        Answer = answer ? string.Empty : null;
        Status = status;
        IsActive = true;
        IsWorking = true;
        _work = new CancellationTokenSource();

        return _work.Token;
    }

    /// <summary>
    /// Ends a search or an answer.
    /// </summary>
    private void End()
    {
        _work?.Dispose();
        _work = null;
        IsWorking = false;
    }

    /// <summary>
    /// Shows a failure; a half-written answer goes.
    /// </summary>
    /// <param name="failure">The failure.</param>
    private void Fail(Exception failure)
    {
        Status = null;
        Answer = null;
        Error = _localizer.Format("Search.Failed", _localizer.Describe(failure));
    }

    /// <summary>
    /// Shows passages.
    /// </summary>
    /// <param name="hits">The passages.</param>
    /// <param name="query">What the user looked for: its words are marked in the passages.</param>
    /// <param name="numbered">Whether they are the numbered sources of an answer.</param>
    private void Show(IReadOnlyList<PassageHit> hits, string query, bool numbered)
    {
        Hits.Clear();
        for (var i = 0; i < hits.Count; i++)
        {
            Hits.Add(new SearchHitViewModel(_localizer, hits[i], numbered ? i + 1 : null, query, OpenAsync));
        }
    }

    /// <summary>
    /// Shows how far the indexing of new passages is.
    /// </summary>
    /// <returns>The receiver of the progress.</returns>
    private Progress<SearchProgress> Indexing()
    {
        return new Progress<SearchProgress>(p =>
        {
            if (IsWorking && p.Done < p.Total)
            {
                Status = _localizer.Format("Search.Indexing", p.Done, p.Total);
            }
        });
    }

    /// <summary>
    /// Opens the record of a passage at the passage: played from it, shown marked with the words of the query.
    /// </summary>
    /// <param name="hit">The passage.</param>
    /// <returns>A task that completes when the window of the record is closed.</returns>
    private async Task OpenAsync(SearchHitViewModel hit)
    {
        Error = null;
        try
        {
            var focus = new RecordFocus(hit.Hit.Start, hit.Hit.End, hit.Query);
            if (!await _viewer.OpenAsync(hit.Hit.RecordId, CancellationToken.None, focus))
            {
                Hits.Remove(hit);
                Error = _localizer.Get("History.Missing");
            }
        }
        catch (IOException ex)
        {
            Error = _localizer.Format("History.OpenFailed", _localizer.Describe(ex));
        }
    }
}
