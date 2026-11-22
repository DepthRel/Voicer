using Voicer.Core.Corrections;
using Voicer.Core.History;
using Voicer.Core.Search;
using Voicer.Localization;

namespace Voicer.UI.Services.History;

/// <summary>
/// The corrections of a record open in its window: what a click or a selection corrects, and the corrections kept in
/// the history before they are shown, so the window never shows a text the history does not have.
/// </summary>
public sealed class RecordCorrector
{
    /// <summary>
    /// The history the corrections are kept in.
    /// </summary>
    private readonly ITranscriptHistory _history;

    /// <summary>
    /// The identifier of the record.
    /// </summary>
    private readonly long _recordId;

    /// <summary>
    /// The clock of the corrections.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// The recognized text with the kept corrections.
    /// </summary>
    private RecordText _text;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordCorrector"/> class.
    /// </summary>
    /// <param name="history">The history the corrections are kept in.</param>
    /// <param name="recordId">The identifier of the record.</param>
    /// <param name="text">The recognized text of the record with its kept corrections.</param>
    /// <param name="time">The clock of the corrections.</param>
    public RecordCorrector(ITranscriptHistory history, long recordId, RecordText text, TimeProvider time)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _recordId = recordId;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        Current = text.Apply();
    }

    /// <summary>
    /// Gets the corrected text of the record with its corrected places.
    /// </summary>
    public CorrectedText Current { get; private set; }

    /// <summary>
    /// Finds what a click or a selection in the corrected text would correct.
    /// </summary>
    /// <param name="at">The clicked character of the corrected text.</param>
    /// <param name="selection">The selection the click is in, or <see langword="null"/> for a click on a word.</param>
    /// <returns>The piece to correct, or <see langword="null"/> when there is nothing to correct there.</returns>
    public CorrectionTarget? Target(int at, TextMark? selection = null)
    {
        return TranscriptCorrections.Target(_text, at, selection);
    }

    /// <summary>
    /// Finds the corrected place at a character of the corrected text.
    /// </summary>
    /// <param name="index">The character, from the beginning of the corrected text.</param>
    /// <returns>
    /// The corrected place, or <see langword="null"/> when the character was not corrected. A deletion is marked on a
    /// character of the text after it, so a correction of that text is preferred.
    /// </returns>
    public CorrectionMark? MarkAt(int index)
    {
        return Current.Marks
            .Where(m => m.Range.Start <= index && index < m.Range.End)
            .OrderBy(m => m.Correction.Replacement.Length == 0)
            .FirstOrDefault();
    }

    /// <summary>
    /// Tells whether a replacement can be put over a piece: a phrase cannot be left without text.
    /// </summary>
    /// <param name="target">The piece to correct.</param>
    /// <param name="replacement">What replaces it.</param>
    /// <returns><see langword="true"/> when the replacement can be put.</returns>
    public bool CanPut(CorrectionTarget target, string replacement)
    {
        return TranscriptCorrections.Put(_text, target, replacement, _time.GetLocalNow()) is not null;
    }

    /// <summary>
    /// Puts a correction over a piece, keeps it in the history and shows it.
    /// </summary>
    /// <param name="target">The piece to correct.</param>
    /// <param name="replacement">What replaces it; empty to delete the piece.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the correction is kept.</returns>
    /// <exception cref="InvalidOperationException">The phrase would be left without text.</exception>
    /// <exception cref="IOException">The history could not keep it, or the record is no longer there.</exception>
    public Task PutAsync(CorrectionTarget target, string replacement, CancellationToken cancellationToken)
    {
        var corrections = TranscriptCorrections.Put(_text, target, replacement, _time.GetLocalNow()) ??
            throw new InvalidOperationException("A correction cannot leave a phrase without text.");

        return SaveAsync(corrections, cancellationToken);
    }

    /// <summary>
    /// Takes a correction back, keeps that in the history and shows the recognized text there.
    /// </summary>
    /// <param name="mark">The corrected place.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the change is kept.</returns>
    /// <exception cref="IOException">The history could not keep it, or the record is no longer there.</exception>
    public Task RevertAsync(CorrectionMark mark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mark);

        return SaveAsync([.. Current.Marks.Select(m => m.Correction).Where(c => c != mark.Correction)], cancellationToken);
    }

    /// <summary>
    /// Keeps the corrections of the record in the history, then shows them.
    /// </summary>
    /// <param name="corrections">The new corrections of the record.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>A task that completes when the corrections are kept.</returns>
    private async Task SaveAsync(IReadOnlyList<TextCorrection> corrections, CancellationToken cancellationToken)
    {
        if (!await _history.SaveCorrectionsAsync(_recordId, corrections, cancellationToken))
        {
            throw LocalizedError.Create(m => new IOException(m), "History.Missing");
        }

        _text = _text with { Corrections = corrections };
        Current = _text.Apply();
    }
}
