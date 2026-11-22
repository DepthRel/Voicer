using Voicer.Core.Enums;
using Voicer.Core.History;
using Voicer.Core.Playback;
using Voicer.Core.Transcripts;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.Services.Summaries;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Services.History;

/// <summary>
/// <see cref="IRecordViewer"/> that reads the text and the sound from the history and shows them in the window of a
/// record, with its summary. The sound lives in a temporary file while the window is open.
/// </summary>
public sealed class RecordViewer : IRecordViewer
{
    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// The history the records are read from.
    /// </summary>
    private readonly ITranscriptHistory _history;

    /// <summary>
    /// The dialogs that show the window and pick where to export.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// The folder of the sound taken out of the history for the player.
    /// </summary>
    private readonly TemporaryAudioFolder _temporary;

    /// <summary>
    /// Creates the player of a sound file of a known length.
    /// </summary>
    private readonly Func<string, TimeSpan, IAudioPlayer> _createPlayer;

    /// <summary>
    /// Writes and keeps the summaries.
    /// </summary>
    private readonly IRecordSummaryService _summaries;

    /// <summary>
    /// The writer of the exported Markdown.
    /// </summary>
    private readonly MarkdownTranscriptWriter _writer;

    /// <summary>
    /// The clock of the corrections.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordViewer"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="history">The history the records are read from.</param>
    /// <param name="dialogs">The dialogs that show the window and pick where to export.</param>
    /// <param name="temporary">The folder of the sound taken out of the history for the player.</param>
    /// <param name="createPlayer">Creates the player of a sound file of a known length.</param>
    /// <param name="summaries">Writes and keeps the summaries.</param>
    /// <param name="time">The clock of the corrections.</param>
    public RecordViewer(
        ILocalizer localizer,
        ITranscriptHistory history,
        IDialogService dialogs,
        TemporaryAudioFolder temporary,
        Func<string, TimeSpan, IAudioPlayer> createPlayer,
        IRecordSummaryService summaries,
        TimeProvider time)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _temporary = temporary ?? throw new ArgumentNullException(nameof(temporary));
        _createPlayer = createPlayer ?? throw new ArgumentNullException(nameof(createPlayer));
        _summaries = summaries ?? throw new ArgumentNullException(nameof(summaries));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _writer = new MarkdownTranscriptWriter(localizer);
    }

    /// <inheritdoc />
    public async Task<bool> OpenAsync(long id, CancellationToken cancellationToken, RecordFocus? focus = null)
    {
        if (await _history.FindTextAsync(id, cancellationToken) is not { } text)
        {
            return false;
        }

        // A generated speech is what was said, so only a recognition is corrected.
        var corrector = text.Original.Kind == RecordKind.Transcription ? new RecordCorrector(_history, id, text, _time) : null;
        var transcript = corrector?.Current.Transcript ?? text.Apply().Transcript;

        var kept = await _history.FindSummaryAsync(id, cancellationToken);
        using var summary = new SummaryPanelViewModel(_localizer, id, transcript, kept, _summaries);

        var file = _temporary.NewFile();
        PlayerViewModel? player = null;
        try
        {
            string? audioNote = null;
            try
            {
                if (await _history.ExportAudioAsync(id, file, cancellationToken) is { } duration)
                {
                    var audio = _createPlayer(file, duration);
                    if (focus is not null)
                    {
                        audio.Seek(focus.Start);
                    }

                    player = new PlayerViewModel(_localizer, audio);
                }
            }
            catch (IOException ex)
            {
                // The text is still worth reading without its sound.
                audioNote = _localizer.Format("Player.LoadFailed", _localizer.Describe(ex));
            }

            await _dialogs.ShowRecordAsync(new RecordViewModel(_localizer, transcript, _writer, _dialogs, player, audioNote, summary, focus, corrector));

            return true;
        }
        finally
        {
            player?.Dispose();
            _temporary.Delete(file);
        }
    }

    /// <inheritdoc />
    public Task ShowAsync(Transcript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        return _dialogs.ShowRecordAsync(new RecordViewModel(_localizer, transcript, _writer, _dialogs));
    }
}
