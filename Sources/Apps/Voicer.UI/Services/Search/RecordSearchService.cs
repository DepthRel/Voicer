using Voicer.Core.Search;
using Voicer.Localization;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Services.Search;

/// <summary>
/// <see cref="IRecordSearchService"/> over the search, the questions and the choice of the user.
/// </summary>
public sealed class RecordSearchService : IRecordSearchService
{
    /// <summary>
    /// How many passages a search shows at most.
    /// </summary>
    private const int RESULTS = 20;

    /// <summary>
    /// The search of the records.
    /// </summary>
    private readonly IRecordSearch _search;

    /// <summary>
    /// Answers questions about the records.
    /// </summary>
    private readonly IRecordQuestions _questions;

    /// <summary>
    /// The choice of the server and the model of the answers.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences;

    /// <summary>
    /// Chooses the model of the search by meaning.
    /// </summary>
    private readonly ITextEmbedderSource _embedders;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSearchService"/> class.
    /// </summary>
    /// <param name="search">The search of the records.</param>
    /// <param name="questions">Answers questions about the records.</param>
    /// <param name="preferences">The choice of the server and the model of the answers.</param>
    /// <param name="embedders">Chooses the model of the search by meaning.</param>
    public RecordSearchService(IRecordSearch search, IRecordQuestions questions, ISummaryPreferencesStore preferences, ITextEmbedderSource embedders)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _questions = questions ?? throw new ArgumentNullException(nameof(questions));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _embedders = embedders ?? throw new ArgumentNullException(nameof(embedders));
    }

    /// <inheritdoc />
    public bool SearchesByMeaning => _embedders.Current() is not null;

    /// <inheritdoc />
    public bool CanAsk => _preferences.Load().ToSettings() is not null;

    /// <inheritdoc />
    public Task<IReadOnlyList<PassageHit>> SearchAsync(string query, IProgress<SearchProgress>? indexing, CancellationToken cancellationToken)
    {
        return _search.SearchAsync(query, _embedders.Current(), RESULTS, indexing, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QuestionAnswer> AskAsync(string question, IProgress<SearchProgress>? indexing, IProgress<string>? answerSoFar, CancellationToken cancellationToken)
    {
        var chat = _preferences.Load().ToSettings() ??
            throw LocalizedError.Create(m => new InvalidOperationException(m), "Summary.NotConfigured");

        return _questions.AskAsync(question, chat, _embedders.Current(), indexing, answerSoFar, cancellationToken);
    }
}
