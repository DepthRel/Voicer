using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Search;
using Voicer.Localization;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A passage the search found, or a source of an answer: its record, its moment and its text; opening it plays the
/// record from that moment.
/// </summary>
public sealed partial class SearchHitViewModel : ObservableObject
{
    /// <summary>
    /// How many characters before the first found word the card shows at most: a found word deep in a long passage
    /// stays within the lines of the card.
    /// </summary>
    private const int EXCERPT_CONTEXT = 80;

    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Opens the record at the passage.
    /// </summary>
    private readonly Func<SearchHitViewModel, Task> _open;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchHitViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="hit">The passage.</param>
    /// <param name="number">The number of the source in an answer, or <see langword="null"/> for a plain result.</param>
    /// <param name="query">What the user looked for: its words are marked in the passage.</param>
    /// <param name="open">Opens the record at the passage.</param>
    public SearchHitViewModel(ILocalizer localizer, PassageHit hit, int? number, string query, Func<SearchHitViewModel, Task> open)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        Hit = hit ?? throw new ArgumentNullException(nameof(hit));
        Number = number;
        Query = query ?? throw new ArgumentNullException(nameof(query));
        _open = open ?? throw new ArgumentNullException(nameof(open));

        Marked = SearchMarks.Excerpt(SearchMarks.Words(hit.Text, FullTextQuery.Prefixes(query)), EXCERPT_CONTEXT);
    }

    /// <summary>
    /// Gets the passage.
    /// </summary>
    public PassageHit Hit { get; }

    /// <summary>
    /// Gets what the user looked for.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Gets the text of the passage as the card shows it: the words of the query marked, and the beginning cut off
    /// when the first of them is far from it.
    /// </summary>
    public MarkedText Marked { get; }

    /// <summary>
    /// Gets the number of the source in an answer, or <see langword="null"/>.
    /// </summary>
    public int? Number { get; }

    /// <summary>
    /// Gets the number of the source as the answer cites it, or an empty text for a plain result.
    /// </summary>
    public string NumberText => Number is { } number ? _localizer.Format("Search.Source", number) : string.Empty;

    /// <summary>
    /// Gets a value indicating whether the passage is a numbered source.
    /// </summary>
    public bool HasNumber => Number is not null;

    /// <summary>
    /// Gets the name of the recognized file.
    /// </summary>
    public string SourceName => Hit.SourceName;

    /// <summary>
    /// Gets when the record was recognized and where in it the passage is.
    /// </summary>
    public string Details => _localizer.Format(
        "Search.HitDetails",
        DisplayText.Moment(_localizer, Hit.CreatedAt),
        DisplayText.Duration(Hit.Start),
        DisplayText.Duration(Hit.End));

    /// <summary>
    /// Words the texts again in the language now chosen.
    /// </summary>
    public void Reword()
    {
        OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Opens the record and plays it from the passage.
    /// </summary>
    /// <returns>A task that completes when the window of the record is closed.</returns>
    [RelayCommand]
    private Task OpenAsync()
    {
        return _open(this);
    }
}
