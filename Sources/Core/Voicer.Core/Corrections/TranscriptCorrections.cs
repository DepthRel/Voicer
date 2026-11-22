using System.Text;
using Voicer.Core.Search;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Corrections;

/// <summary>
/// Applies the corrections of a record to its recognized text, finds what a click or a selection corrects, and joins a
/// new correction with the ones before it. The corrections come from the file of the history, so a correction that does
/// not fit the text is left out instead of breaking the record.
/// </summary>
public static class TranscriptCorrections
{
    /// <summary>
    /// Applies corrections to a recognized record.
    /// </summary>
    /// <param name="original">The record as it was recognized.</param>
    /// <param name="corrections">The corrections, in any order.</param>
    /// <returns>The corrected record and the places the corrections changed.</returns>
    public static CorrectedText Apply(Transcript original, IReadOnlyList<TextCorrection> corrections)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(corrections);

        var fitting = Fitting(original, corrections);
        if (fitting.Count == 0)
        {
            return new CorrectedText(original, []);
        }

        var segments = original.Segments.ToList();
        var layouts = new Dictionary<int, PhraseLayout>();
        foreach (var group in fitting.GroupBy(c => c.Segment))
        {
            var layout = Layout(original.Segments[group.Key].Text.Trim(), [.. group]);
            layouts[group.Key] = layout;
            segments[group.Key] = segments[group.Key] with { Text = layout.Shown };
        }

        var transcript = original with { Segments = segments };

        return new CorrectedText(transcript, Marks(transcript, layouts));
    }

    /// <summary>
    /// Finds the word at a place of a text: letters and digits, with a hyphen or an apostrophe between two of them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The place, in characters from the beginning of the text.</param>
    /// <returns>The word, or <see langword="null"/> when there is none at the place.</returns>
    public static TextMark? WordAt(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (index < 0 || index >= text.Length || !IsWordCharacter(text, index))
        {
            return null;
        }

        var start = index;
        while (start > 0 && IsWordCharacter(text, start - 1))
        {
            start--;
        }

        var end = index + 1;
        while (end < text.Length && IsWordCharacter(text, end))
        {
            end++;
        }

        return new TextMark(start, end - start);
    }

    /// <summary>
    /// Finds what a correction would replace: the word under a click, or a selection. A selection is kept to the phrase it
    /// starts in, without spaces at its edges, and takes in every correction it touches whole.
    /// </summary>
    /// <param name="text">The record with its corrections.</param>
    /// <param name="at">The clicked character of the corrected text.</param>
    /// <param name="selection">The selection the click is in, or <see langword="null"/> for a click on a word.</param>
    /// <returns>The piece to correct, or <see langword="null"/> when there is nothing to correct there.</returns>
    public static CorrectionTarget? Target(RecordText text, int at, TextMark? selection = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var corrected = text.Apply().Transcript;
        if (selection is not { Length: > 0 } chosen)
        {
            if (WordAt(corrected.Text, at) is not { } word)
            {
                return null;
            }

            chosen = word;
        }

        // The phrase is the clicked one; a menu asked from the keyboard has no click in the text, so the first phrase the
        // selection reaches is taken.
        var ranges = TranscriptLayout.Ranges(corrected);
        var segment = Enumerable.Range(0, ranges.Count)
            .FirstOrDefault(i => ranges[i] is { } range && range.Start <= at && at < range.End, -1);
        if (segment < 0)
        {
            segment = Enumerable.Range(0, ranges.Count)
                .FirstOrDefault(i => ranges[i] is { } range && range.Start < chosen.End && range.End > chosen.Start, -1);
        }

        if (segment < 0)
        {
            return null;
        }

        var phrase = ranges[segment]!.Value;
        var layout = Layout(text.Original.Segments[segment].Text.Trim(), [.. Fitting(text.Original, text.Corrections).Where(c => c.Segment == segment)]);
        var from = Math.Max(chosen.Start, phrase.Start) - phrase.Start;
        var to = Math.Min(chosen.End, phrase.End) - phrase.Start;
        if (from >= to)
        {
            return null;
        }

        while (from < to && char.IsWhiteSpace(layout.Shown[from]))
        {
            from++;
        }

        while (to > from && char.IsWhiteSpace(layout.Shown[to - 1]))
        {
            to--;
        }

        if (from == to)
        {
            return null;
        }

        foreach (var piece in layout.Pieces.Where(p => p.Correction is not null && p.ShownStart < to && p.ShownEnd > from))
        {
            from = Math.Min(from, piece.ShownStart);
            to = Math.Max(to, piece.ShownEnd);
        }

        var originalStart = OriginalStart(layout, from);
        var originalEnd = OriginalEnd(layout, to);
        if (originalEnd <= originalStart)
        {
            return null;
        }

        return new CorrectionTarget(segment, originalStart, originalEnd - originalStart, new TextMark(phrase.Start + from, to - from), layout.Shown[from..to]);
    }

    /// <summary>
    /// Puts a correction over a piece of a record. The corrections it overlaps are replaced by it, and the piece it
    /// replaces stays the recognized text; putting the recognized text back removes the correction. An empty replacement
    /// deletes the piece with one space next to it.
    /// </summary>
    /// <param name="text">The record with its corrections.</param>
    /// <param name="target">The piece to correct, from <see cref="Target"/>.</param>
    /// <param name="replacement">What replaces it; spaces at its edges are dropped.</param>
    /// <param name="now">When the correction is made.</param>
    /// <returns>
    /// The corrections of the record after it, in their order; <see langword="null"/> when the phrase would be left without
    /// text.
    /// </returns>
    public static IReadOnlyList<TextCorrection>? Put(RecordText text, CorrectionTarget target, string replacement, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentOutOfRangeException.ThrowIfNegative(target.Segment);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(target.Segment, text.Original.Segments.Count);

        var recognized = text.Original.Segments[target.Segment].Text.Trim();
        var fitting = Fitting(text.Original, text.Corrections);
        var others = fitting.Where(c => c.Segment == target.Segment).ToList();
        var start = Math.Clamp(target.Start, 0, recognized.Length);
        var end = Math.Clamp(target.End, start, recognized.Length);
        var absorbed = others.Where(c => c.Start < end && c.End > start).ToList();
        start = absorbed.Select(c => c.Start).Append(start).Min();
        end = absorbed.Select(c => c.End).Append(end).Max();
        var wanted = replacement.Trim();

        if (wanted.Length == 0)
        {
            bool IsFreeSpace(int i) => char.IsWhiteSpace(recognized[i]) && !others.Any(c => c.Start <= i && i < c.End);

            if (start > 0 && IsFreeSpace(start - 1))
            {
                start--;
            }
            else if (end < recognized.Length && IsFreeSpace(end))
            {
                end++;
            }
        }

        var corrections = fitting.Except(absorbed).ToList();
        var piece = recognized[start..end];
        if (!string.Equals(piece, wanted, StringComparison.Ordinal))
        {
            corrections.Add(new TextCorrection(target.Segment, start, end - start, piece, wanted, now));
        }

        corrections = [.. corrections.OrderBy(c => c.Segment).ThenBy(c => c.Start)];
        var result = Apply(text.Original, corrections).Transcript;

        return result.Segments[target.Segment].Text.Trim().Length == 0 ? null : corrections;
    }

    /// <summary>
    /// Keeps the corrections that fit the recognized text: in a phrase of the record, inside its trimmed text, over the
    /// piece they say they replace, and not over another correction.
    /// </summary>
    /// <param name="original">The record as it was recognized.</param>
    /// <param name="corrections">The corrections.</param>
    /// <returns>The fitting corrections, ordered by phrase and place.</returns>
    private static List<TextCorrection> Fitting(Transcript original, IReadOnlyList<TextCorrection> corrections)
    {
        var fitting = new List<TextCorrection>();
        var ordered = corrections.OrderBy(c => c.Segment).ThenBy(c => c.Start);
        foreach (var correction in ordered)
        {
            if (correction.Segment < 0 || correction.Segment >= original.Segments.Count || correction.Start < 0 ||
                correction.Length <= 0 || correction.Original is null || correction.Replacement is null)
            {
                continue;
            }

            var text = original.Segments[correction.Segment].Text.Trim();
            if (correction.End > text.Length ||
                !string.Equals(text.Substring(correction.Start, correction.Length), correction.Original, StringComparison.Ordinal))
            {
                continue;
            }

            if (fitting.Count > 0 && fitting[^1].Segment == correction.Segment && fitting[^1].End > correction.Start)
            {
                continue;
            }

            fitting.Add(correction);
        }

        return fitting;
    }

    /// <summary>
    /// Lays a phrase out as the pieces of its recognized text, kept or corrected, and where each is shown.
    /// </summary>
    /// <param name="recognized">The trimmed recognized text of the phrase.</param>
    /// <param name="corrections">The fitting corrections of the phrase, in their order.</param>
    /// <returns>The corrected phrase and its pieces.</returns>
    private static PhraseLayout Layout(string recognized, IReadOnlyList<TextCorrection> corrections)
    {
        var built = new StringBuilder();
        var pieces = new List<Piece>();
        var from = 0;

        foreach (var correction in corrections)
        {
            if (correction.Start > from)
            {
                pieces.Add(new Piece(from, correction.Start, built.Length, built.Length + correction.Start - from, null));
                built.Append(recognized, from, correction.Start - from);
            }

            pieces.Add(new Piece(correction.Start, correction.End, built.Length, built.Length + correction.Replacement.Length, correction));
            built.Append(correction.Replacement);
            from = correction.End;
        }

        if (from < recognized.Length)
        {
            pieces.Add(new Piece(from, recognized.Length, built.Length, built.Length + recognized.Length - from, null));
            built.Append(recognized, from, recognized.Length - from);
        }

        // The corrected phrase is trimmed like a recognized one, so what it loses at its beginning moves every piece.
        var whole = built.ToString();
        var shown = whole.Trim();
        var lead = whole.Length - whole.TrimStart().Length;
        var shifted = pieces.Select(p => Trimmed(p, lead, shown.Length)).ToList();

        return new PhraseLayout(shown, shifted);
    }

    /// <summary>
    /// Moves a piece into the trimmed corrected phrase. A kept piece that loses spaces at an edge of the phrase loses
    /// them in the recognized text too, so its places stay one to one.
    /// </summary>
    /// <param name="piece">The piece, placed in the corrected phrase before it was trimmed.</param>
    /// <param name="lead">How many characters the trimming took from the beginning.</param>
    /// <param name="length">The length of the trimmed corrected phrase.</param>
    /// <returns>The piece placed in the trimmed corrected phrase.</returns>
    private static Piece Trimmed(Piece piece, int lead, int length)
    {
        var start = piece.ShownStart - lead;
        var end = piece.ShownEnd - lead;
        var originalStart = piece.OriginalStart;
        var originalEnd = piece.OriginalEnd;

        if (piece.Correction is null)
        {
            originalStart += Math.Clamp(-start, 0, originalEnd - originalStart);
            originalEnd -= Math.Clamp(end - length, 0, originalEnd - originalStart);
        }

        return piece with
        {
            OriginalStart = originalStart,
            OriginalEnd = originalEnd,
            ShownStart = Math.Clamp(start, 0, length),
            ShownEnd = Math.Clamp(end, 0, length),
        };
    }

    /// <summary>
    /// Finds the corrected places in the text of the corrected record.
    /// </summary>
    /// <param name="transcript">The corrected record.</param>
    /// <param name="layouts">The layouts of the corrected phrases, by their index.</param>
    /// <returns>The marks, in their order; a phrase left without text has none.</returns>
    private static List<CorrectionMark> Marks(Transcript transcript, Dictionary<int, PhraseLayout> layouts)
    {
        var ranges = TranscriptLayout.Ranges(transcript);
        var marks = new List<CorrectionMark>();
        foreach (var (segment, layout) in layouts.OrderBy(l => l.Key))
        {
            if (ranges[segment] is not { } phrase)
            {
                continue;
            }

            foreach (var piece in layout.Pieces.Where(p => p.Correction is not null))
            {
                var at = phrase.Start + piece.ShownStart;
                var length = piece.ShownEnd - piece.ShownStart;
                if (length == 0)
                {
                    // A deletion leaves no text of its own: one character where it was stands for it.
                    length = 1;
                    at = Math.Min(at, phrase.End - 1);
                }

                marks.Add(new CorrectionMark(new TextMark(at, length), piece.Correction!));
            }
        }

        return marks;
    }

    /// <summary>
    /// Finds where a piece that starts at a shown place starts in the recognized text.
    /// </summary>
    /// <param name="layout">The layout of the phrase.</param>
    /// <param name="shown">The place in the corrected phrase.</param>
    /// <returns>The place in the trimmed recognized phrase.</returns>
    private static int OriginalStart(PhraseLayout layout, int shown)
    {
        var piece = layout.Pieces.First(p => p.ShownStart <= shown && shown < p.ShownEnd);

        return piece.Correction is null ? piece.OriginalStart + shown - piece.ShownStart : piece.OriginalStart;
    }

    /// <summary>
    /// Finds where a piece that ends at a shown place ends in the recognized text.
    /// </summary>
    /// <param name="layout">The layout of the phrase.</param>
    /// <param name="shown">The place after the end of the piece in the corrected phrase.</param>
    /// <returns>The place after the end of the piece in the trimmed recognized phrase.</returns>
    private static int OriginalEnd(PhraseLayout layout, int shown)
    {
        var piece = layout.Pieces.Last(p => p.ShownStart < shown && shown <= p.ShownEnd);

        return piece.Correction is null ? piece.OriginalStart + shown - piece.ShownStart : piece.OriginalEnd;
    }

    /// <summary>
    /// Tells whether a character belongs to a word: a letter or a digit, or a hyphen or an apostrophe between two of them.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The place of the character.</param>
    /// <returns><see langword="true"/> for a character of a word.</returns>
    private static bool IsWordCharacter(string text, int index)
    {
        var character = text[index];
        if (char.IsLetterOrDigit(character))
        {
            return true;
        }

        return character is '-' or '\'' or '’' &&
            index > 0 && index < text.Length - 1 &&
            char.IsLetterOrDigit(text[index - 1]) && char.IsLetterOrDigit(text[index + 1]);
    }

    /// <summary>
    /// A piece of a phrase: kept as recognized, or replaced by a correction.
    /// </summary>
    /// <param name="OriginalStart">Where it starts in the trimmed recognized phrase.</param>
    /// <param name="OriginalEnd">Where it ends there.</param>
    /// <param name="ShownStart">Where it starts in the corrected phrase.</param>
    /// <param name="ShownEnd">Where it ends there.</param>
    /// <param name="Correction">The correction that replaced it, or <see langword="null"/> when it is kept.</param>
    private sealed record Piece(int OriginalStart, int OriginalEnd, int ShownStart, int ShownEnd, TextCorrection? Correction);

    /// <summary>
    /// A phrase with its corrections applied.
    /// </summary>
    /// <param name="Shown">The corrected, trimmed text of the phrase.</param>
    /// <param name="Pieces">Its pieces, in their order.</param>
    private sealed record PhraseLayout(string Shown, IReadOnlyList<Piece> Pieces);
}
