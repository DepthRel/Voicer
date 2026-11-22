using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.History;
using Voicer.Core.Summaries;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.Summaries;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// The summary of a record in its window: the kept one, or none yet, and the button that writes it (again); the summary
/// appears as the model writes it.
/// </summary>
public sealed partial class SummaryPanelViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// The identifier of the record.
    /// </summary>
    private readonly long _recordId;

    /// <summary>
    /// The text of the record; it changes when the user corrects it.
    /// </summary>
    private Transcript _transcript;

    /// <summary>
    /// Writes and keeps the summary.
    /// </summary>
    private readonly IRecordSummaryService _summaries;

    /// <summary>
    /// The kept summary, or <see langword="null"/>.
    /// </summary>
    private RecordSummary? _kept;

    /// <summary>
    /// Stops the summary being written, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _work;

    /// <summary>
    /// The summary shown: the kept one, or the one being written.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    private string _text = string.Empty;

    /// <summary>
    /// Which model wrote the shown summary and when, or what is being done.
    /// </summary>
    [ObservableProperty]
    private string _details = string.Empty;

    /// <summary>
    /// Whether a summary is being written.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummarizeLabel))]
    [NotifyCanExecuteChangedFor(nameof(SummarizeCommand), nameof(CancelCommand))]
    private bool _isWorking;

    /// <summary>
    /// Why the last summary failed, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryPanelViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <param name="transcript">The text of the record.</param>
    /// <param name="kept">The kept summary, or <see langword="null"/>.</param>
    /// <param name="summaries">Writes and keeps the summary.</param>
    public SummaryPanelViewModel(ILocalizer localizer, long recordId, Transcript transcript, RecordSummary? kept, IRecordSummaryService summaries)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _recordId = recordId;
        _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        Show(kept);
    }

    /// <summary>
    /// Gets a value indicating whether a summary is shown.
    /// </summary>
    public bool HasText => Text.Length > 0;

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets the kept summary, or <see langword="null"/>; it goes into the export.
    /// </summary>
    public string? KeptText => _kept?.Text;

    /// <summary>
    /// Gets the text of the button: to write a summary, or to write it again.
    /// </summary>
    public string SummarizeLabel => _localizer.Get(_kept is null ? "Record.Summarize" : "Record.Resummarize");

    /// <summary>
    /// Takes the corrected text of the record: the next summary is written from it. The kept summary stays.
    /// </summary>
    /// <param name="transcript">The text of the record.</param>
    public void UseText(Transcript transcript)
    {
        _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _work?.Cancel();
    }

    /// <summary>
    /// Writes the summary with the model of the settings and keeps it; a failure or a stop leaves the kept one.
    /// </summary>
    /// <returns>A task that completes when the summary is written, failed or was stopped.</returns>
    [RelayCommand(CanExecute = nameof(CanSummarize))]
    private async Task SummarizeAsync()
    {
        Error = null;
        if (!_summaries.IsConfigured)
        {
            Error = _localizer.Get("Summary.NotConfigured");

            return;
        }

        IsWorking = true;
        Text = string.Empty;
        Details = _localizer.Format("Record.Summarizing", 1, 1);
        _work = new CancellationTokenSource();
        var progress = new Progress<SummaryProgress>(p =>
        {
            if (!IsWorking)
            {
                return;
            }

            Details = _localizer.Format("Record.Summarizing", p.Step, p.Steps);
            if (p.Text is not null)
            {
                Text = p.Text;
            }
        });

        try
        {
            _kept = await _summaries.SummarizeAsync(_recordId, _transcript, progress, _work.Token);
        }
        catch (OperationCanceledException)
        {
            // Stopped: the kept summary stays.
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException or InvalidOperationException or Win32Exception)
        {
            Error = _localizer.Format("Summary.Failed", _localizer.Describe(ex));
        }
        finally
        {
            _work.Dispose();
            _work = null;
            IsWorking = false;
            Show(_kept);
        }
    }

    /// <summary>
    /// Tells whether a summary can be written now.
    /// </summary>
    /// <returns><see langword="true"/> when none is being written.</returns>
    private bool CanSummarize()
    {
        return !IsWorking;
    }

    /// <summary>
    /// Stops the summary being written.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsWorking))]
    private void Cancel()
    {
        _work?.Cancel();
    }

    /// <summary>
    /// Shows a kept summary, or that there is none.
    /// </summary>
    /// <param name="summary">The summary, or <see langword="null"/>.</param>
    private void Show(RecordSummary? summary)
    {
        _kept = summary;
        Text = summary?.Text ?? string.Empty;
        Details = summary is null
            ? _localizer.Get("Record.NoSummary")
            : _localizer.Format("Record.SummaryDetails", summary.ModelName, DisplayText.Moment(_localizer, summary.CreatedAt));
        OnPropertyChanged(nameof(SummarizeLabel));
        OnPropertyChanged(nameof(KeptText));
    }
}
