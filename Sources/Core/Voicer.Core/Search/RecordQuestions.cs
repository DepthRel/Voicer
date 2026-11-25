using Voicer.Core.Enums;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.Core.Search;

/// <summary>
/// <see cref="IRecordQuestions"/> over the search of the records and a server of language models. The passages that
/// fit into the context are given to the model, numbered, with their file, date and time; the instructions, in the
/// language of the interface, tell it to answer from them only and to cite them by number.
/// </summary>
public sealed class RecordQuestions : IRecordQuestions
{
    /// <summary>
    /// How many passages are given to the model at most.
    /// </summary>
    private const int MAX_SOURCES = 8;

    /// <summary>
    /// The search of the records.
    /// </summary>
    private readonly IRecordSearch _search;

    /// <summary>
    /// The client of the server of language models.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// The localizer of the instructions.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordQuestions"/> class.
    /// </summary>
    /// <param name="search">The search of the records.</param>
    /// <param name="client">The client of the server of language models.</param>
    /// <param name="localizer">The localizer of the instructions.</param>
    public RecordQuestions(IRecordSearch search, ILanguageModelClient client, ILocalizer localizer)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    /// <inheritdoc />
    public async Task<QuestionAnswer> AskAsync(
        string question,
        SummarySettings chat,
        ITextEmbedder? embedder,
        IProgress<SearchProgress>? indexing,
        IProgress<string>? answerSoFar,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(chat);

        var hits = await _search.SearchAsync(question, embedder, MAX_SOURCES, indexing, cancellationToken);
        var sources = Fit(hits, Summarizer.PartTokens(chat.ContextTokens));
        if (sources.Count == 0)
        {
            return new QuestionAnswer(_localizer.Get("Ask.NothingFound"), []);
        }

        ChatMessage[] messages =
        [
            new(ChatRole.System, _localizer.Get("Ask.Prompt.System")),
            new(ChatRole.User, _localizer.Format("Ask.Prompt.Question", question.Trim(), Describe(sources))),
        ];
        var arriving = answerSoFar is null ? null : new FilteredProgress(answerSoFar);

        var answer = ReasoningFilter.Strip(await _client.CompleteAsync(chat.Endpoint, chat.Model, messages, arriving, cancellationToken));
        if (answer.Length == 0)
        {
            throw LocalizedError.Create(m => new InvalidDataException(m), "Summary.EmptyAnswer", chat.Model);
        }

        return new QuestionAnswer(answer, sources);
    }

    /// <summary>
    /// Keeps the best passages that fit into one request together.
    /// </summary>
    /// <param name="hits">The passages, the best first.</param>
    /// <param name="limit">How many tokens of passages fit, by the estimate of <see cref="TokenBudget"/>.</param>
    /// <returns>The passages that fit, in their order; at least the first one, cut to the limit.</returns>
    private static List<PassageHit> Fit(IReadOnlyList<PassageHit> hits, int limit)
    {
        var fitted = new List<PassageHit>();
        var used = 0;
        foreach (var hit in hits)
        {
            var tokens = TokenBudget.Estimate(hit.Text);
            if (fitted.Count > 0 && used + tokens > limit)
            {
                break;
            }

            fitted.Add(tokens <= limit ? hit : hit with { Text = TokenBudget.Cut(hit.Text, limit) });
            used += Math.Min(tokens, limit);
        }

        return fitted;
    }

    /// <summary>
    /// Writes the numbered sources for the model.
    /// </summary>
    /// <param name="sources">The passages.</param>
    /// <returns>The sources, a blank line between them.</returns>
    private string Describe(IReadOnlyList<PassageHit> sources)
    {
        return string.Join(
            "\n\n",
            sources.Select((hit, i) => _localizer.Format(
                "Ask.Source",
                i + 1,
                hit.SourceName,
                hit.CreatedAt.ToString("g", _localizer.Culture),
                TimeCode.Format(hit.Start),
                TimeCode.Format(hit.End),
                hit.Text)));
    }

    /// <summary>
    /// Passes the answer as it arrives without the reasoning of a thinking model.
    /// </summary>
    /// <param name="target">Where the answer goes.</param>
    private sealed class FilteredProgress(IProgress<string> target) : IProgress<string>
    {
        /// <inheritdoc />
        public void Report(string value) => target.Report(ReasoningFilter.Strip(value));
    }
}
