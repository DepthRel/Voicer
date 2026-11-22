using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.UI.Services.Summaries;

/// <summary>
/// <see cref="IRecordSummaryService"/> over the summarizer, the choice of the user and the history.
/// </summary>
public sealed class RecordSummaryService : IRecordSummaryService
{
    /// <summary>
    /// Writes the summaries.
    /// </summary>
    private readonly ISummarizer _summarizer;

    /// <summary>
    /// The chosen model of the summaries: of the server, or of Voicer.
    /// </summary>
    private readonly IChatModelSource _chat;

    /// <summary>
    /// The history the summaries are kept in.
    /// </summary>
    private readonly ITranscriptHistory _history;

    /// <summary>
    /// The clock that dates the summaries.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordSummaryService"/> class.
    /// </summary>
    /// <param name="summarizer">Writes the summaries.</param>
    /// <param name="chat">The chosen model of the summaries: of the server, or of Voicer.</param>
    /// <param name="history">The history the summaries are kept in.</param>
    /// <param name="time">The clock that dates the summaries; the system clock by default.</param>
    public RecordSummaryService(ISummarizer summarizer, IChatModelSource chat, ITranscriptHistory history, TimeProvider? time = null)
    {
        _summarizer = summarizer ?? throw new ArgumentNullException(nameof(summarizer));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsConfigured => _chat.IsAvailable;

    /// <inheritdoc />
    public async Task<RecordSummary> SummarizeAsync(long id, Transcript transcript, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        using var session = await _chat.OpenAsync(cancellationToken);
        var text = await session.RunAsync(settings => _summarizer.SummarizeAsync(transcript, settings, progress, cancellationToken));
        var summary = new RecordSummary(text, session.Settings.Model, _time.GetLocalNow());

        if (!await _history.SaveSummaryAsync(id, summary, CancellationToken.None))
        {
            throw LocalizedError.Create(m => new IOException(m), "History.Missing");
        }

        return summary;
    }
}
