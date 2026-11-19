using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.Configuration;

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
    /// The choice of the server and the model.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences;

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
    /// <param name="preferences">The choice of the server and the model.</param>
    /// <param name="history">The history the summaries are kept in.</param>
    /// <param name="time">The clock that dates the summaries; the system clock by default.</param>
    public RecordSummaryService(ISummarizer summarizer, ISummaryPreferencesStore preferences, ITranscriptHistory history, TimeProvider? time = null)
    {
        _summarizer = summarizer ?? throw new ArgumentNullException(nameof(summarizer));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public bool IsConfigured => _preferences.Load().ToSettings() is not null;

    /// <inheritdoc />
    public async Task<RecordSummary> SummarizeAsync(long id, Transcript transcript, IProgress<SummaryProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var settings = _preferences.Load().ToSettings() ??
            throw LocalizedError.Create(m => new InvalidOperationException(m), "Summary.NotConfigured");

        var text = await _summarizer.SummarizeAsync(transcript, settings, progress, cancellationToken);
        var summary = new RecordSummary(text, settings.Model, _time.GetLocalNow());

        if (!await _history.SaveSummaryAsync(id, summary, CancellationToken.None))
        {
            throw LocalizedError.Create(m => new IOException(m), "History.Missing");
        }

        return summary;
    }
}
