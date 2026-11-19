using Voicer.Core.Enums;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.Core.Summaries;

/// <summary>
/// <see cref="ISummarizer"/> that fits the text to the context of the model: a short text is summarized at once; a
/// long one part by part, then the notes of the parts are joined into one summary (the notes themselves are joined by
/// groups first when they are too long together).
/// </summary>
/// <remarks>
/// The instructions to the model come from the language pack, so the summary is written in the language of the
/// interface, whatever the language of the speech.
/// </remarks>
public sealed class Summarizer : ISummarizer
{
    /// <summary>
    /// The tokens kept free for the answer of the model.
    /// </summary>
    private const int ANSWER_TOKENS = 1024;

    /// <summary>
    /// The tokens kept free for the instructions around the text.
    /// </summary>
    private const int INSTRUCTION_TOKENS = 512;

    /// <summary>
    /// How many characters of text a token holds at least: about 2.5 in Russian, 4 in English; the smaller value keeps
    /// every language inside the context.
    /// </summary>
    private const double CHARS_PER_TOKEN = 2.5;

    /// <summary>
    /// How many times notes are joined by groups at most; a model that does not shorten them gives up the rest.
    /// </summary>
    private const int MAX_JOIN_ROUNDS = 3;

    /// <summary>
    /// The client of the server of language models.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// The localizer of the instructions and the failures.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="Summarizer"/> class.
    /// </summary>
    /// <param name="client">The client of the server of language models.</param>
    /// <param name="localizer">The localizer of the instructions and the failures.</param>
    public Summarizer(ILanguageModelClient client, ILocalizer localizer)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    /// <summary>
    /// Tells how many characters of text fit into one request.
    /// </summary>
    /// <param name="contextTokens">The context of the model, in tokens.</param>
    /// <returns>The largest part of the text.</returns>
    public static int PartLength(int contextTokens)
    {
        var tokens = Math.Clamp(contextTokens, SummarySettings.MIN_CONTEXT_TOKENS, SummarySettings.MAX_CONTEXT_TOKENS);

        return (int)((tokens - ANSWER_TOKENS - INSTRUCTION_TOKENS) * CHARS_PER_TOKEN);
    }

    /// <inheritdoc />
    public async Task<string> SummarizeAsync(Transcript transcript, SummarySettings settings, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentNullException.ThrowIfNull(settings);

        var text = transcript.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw LocalizedError.Create(m => new InvalidDataException(m), "Summary.NoText");
        }

        var limit = PartLength(settings.ContextTokens);
        var parts = TextChunker.Split(text, limit);
        if (parts.Count == 1)
        {
            return await FinalAsync(settings, Prompt("Summary.Prompt.Whole", transcript.SourceName, text), 1, 1, progress, cancellationToken);
        }

        // Every part is read on its own, then the notes are joined; notes too long together are joined by groups first.
        var steps = parts.Count + 1;
        var step = 0;
        var notes = new List<string>();
        for (var i = 0; i < parts.Count; i++)
        {
            progress?.Report(new SummaryProgress(++step, steps, null));
            var prompt = Prompt("Summary.Prompt.Part", i + 1, parts.Count, transcript.SourceName, parts[i]);
            notes.Add(await AskAsync(settings, prompt, null, cancellationToken));
        }

        var groups = Group(notes, limit);
        for (var round = 0; groups.Count > 1 && round < MAX_JOIN_ROUNDS; round++)
        {
            steps += groups.Count;
            var joined = new List<string>();
            foreach (var group in groups)
            {
                progress?.Report(new SummaryProgress(++step, steps, null));
                joined.Add(await AskAsync(settings, Prompt("Summary.Prompt.Combine", transcript.SourceName, group), null, cancellationToken));
            }

            groups = Group(joined, limit);
        }

        // Notes a model would not shorten are cut to the context rather than overflow it.
        var final = string.Join("\n\n", groups);
        final = final.Length <= limit ? final : final[..limit];

        return await FinalAsync(settings, Prompt("Summary.Prompt.Combine", transcript.SourceName, final), steps, steps, progress, cancellationToken);
    }

    /// <summary>
    /// Makes the last request, whose answer is the summary, and reports it as it arrives.
    /// </summary>
    /// <param name="settings">The server, the model and its context.</param>
    /// <param name="prompt">The request.</param>
    /// <param name="step">The number of the request.</param>
    /// <param name="steps">How many requests the summary takes.</param>
    /// <param name="progress">Receives the summary as it arrives.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The summary.</returns>
    private async Task<string> FinalAsync(SummarySettings settings, string prompt, int step, int steps, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new SummaryProgress(step, steps, string.Empty));
        var arriving = progress is null ? null : new SyncReport(text => progress.Report(new SummaryProgress(step, steps, ReasoningFilter.Strip(text))));

        return await AskAsync(settings, prompt, arriving, cancellationToken);
    }

    /// <summary>
    /// Asks the model one request.
    /// </summary>
    /// <param name="settings">The server and the model.</param>
    /// <param name="prompt">The request.</param>
    /// <param name="answerSoFar">Receives the answer as it arrives, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The answer without the reasoning.</returns>
    /// <exception cref="InvalidDataException">The model gave no answer.</exception>
    private async Task<string> AskAsync(SummarySettings settings, string prompt, IProgress<string>? answerSoFar, CancellationToken cancellationToken)
    {
        ChatMessage[] messages =
        [
            new(ChatRole.System, _localizer.Get("Summary.Prompt.System")),
            new(ChatRole.User, prompt),
        ];

        var answer = ReasoningFilter.Strip(await _client.CompleteAsync(settings.Endpoint, settings.Model, messages, answerSoFar, cancellationToken));
        if (answer.Length == 0)
        {
            throw LocalizedError.Create(m => new InvalidDataException(m), "Summary.EmptyAnswer", settings.Model);
        }

        return answer;
    }

    /// <summary>
    /// Puts notes together, a blank line between them, in as few groups as fit into one request.
    /// </summary>
    /// <param name="notes">The notes, in their order.</param>
    /// <param name="limit">The largest group, in characters.</param>
    /// <returns>The groups; one when all the notes fit.</returns>
    private static IReadOnlyList<string> Group(IReadOnlyList<string> notes, int limit)
    {
        var joined = string.Join("\n\n", notes);

        return joined.Length <= limit ? [joined] : TextChunker.Split(joined, limit);
    }

    /// <summary>
    /// Words an instruction to the model in the language of the interface.
    /// </summary>
    /// <param name="key">The key of the instruction.</param>
    /// <param name="args">Its values.</param>
    /// <returns>The instruction.</returns>
    private string Prompt(string key, params object[] args)
    {
        return _localizer.Format(key, args);
    }

    /// <summary>
    /// Passes the answer as it arrives straight to a callback, on the thread it arrives on.
    /// </summary>
    /// <param name="report">The callback.</param>
    private sealed class SyncReport(Action<string> report) : IProgress<string>
    {
        /// <inheritdoc />
        public void Report(string value) => report(value);
    }
}
