using System.Text;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Search;

/// <summary>
/// Finds what to mark in a found text: the words that match the query the way the full-text index matched them
/// (<see cref="FullTextQuery.Prefixes"/>), the part of a long passage worth showing, and the passage in the text of its
/// whole record.
/// </summary>
public static class SearchMarks
{
    /// <summary>
    /// The sign that stands for the text left out at the beginning of an excerpt.
    /// </summary>
    private const string ELLIPSIS = "…";

    /// <summary>
    /// Marks the words of a text that start with one of the beginnings of a query.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="prefixes">The beginnings, in lower case.</param>
    /// <returns>The text with its matching words marked.</returns>
    public static MarkedText Words(string text, IReadOnlyList<string> prefixes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(prefixes);

        var marks = new List<TextMark>();
        if (prefixes.Count == 0)
        {
            return new MarkedText(text, marks);
        }

        var start = -1;
        var length = 1;
        for (var i = 0; i <= text.Length; i += length)
        {
            // A word is read by whole characters, as the index reads it: a pair of surrogates and a mark belong to it.
            var inWord = false;
            length = 1;
            if (i < text.Length)
            {
                Rune.DecodeFromUtf16(text.AsSpan(i), out var character, out length);
                inWord = WordCharacters.Contains(character);
            }

            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                // The index lowers every letter on its own, so the word keeps its length.
                var word = text[start..i].ToLowerInvariant();
                if (prefixes.Any(prefix => word.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    marks.Add(new TextMark(start, i - start));
                }

                start = -1;
            }
        }

        return new MarkedText(text, marks);
    }

    /// <summary>
    /// Cuts the beginning of a long text off when its first marked word is far from it, so a short view of the text
    /// shows the word: a few words before it are kept and the cut is shown by an ellipsis.
    /// </summary>
    /// <param name="marked">The text with its marked words; a marked passage is not kept.</param>
    /// <param name="context">How many characters before the first marked word are kept at most.</param>
    /// <returns>The excerpt, or the text as it is when its first marked word is close to its beginning.</returns>
    public static MarkedText Excerpt(MarkedText marked, int context)
    {
        ArgumentNullException.ThrowIfNull(marked);
        ArgumentOutOfRangeException.ThrowIfNegative(context);

        if (marked.Words.Count == 0 || marked.Words[0].Start <= context + ELLIPSIS.Length)
        {
            return marked;
        }

        // The cut moves forward to the beginning of a word, never into one.
        var first = marked.Words[0].Start;
        var cut = first - context;
        while (cut < first && !char.IsWhiteSpace(marked.Text[cut - 1]))
        {
            cut++;
        }

        var shift = ELLIPSIS.Length - cut;

        return new MarkedText(
            ELLIPSIS + marked.Text[cut..],
            [.. marked.Words.Select(word => word with { Start = word.Start + shift })]);
    }

    /// <summary>
    /// Finds a passage in the text of its record (<see cref="Transcript.Text"/>): the pieces of speech whose middle lies
    /// between the moments of the passage, so a moment rounded by the storage does not take in a neighbour.
    /// </summary>
    /// <param name="transcript">The record.</param>
    /// <param name="start">Where the passage starts in the recording.</param>
    /// <param name="end">Where it ends.</param>
    /// <returns>The passage in the text, or <see langword="null"/> when no piece of speech lies there.</returns>
    public static TextMark? Passage(Transcript transcript, TimeSpan start, TimeSpan end)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var separator = Environment.NewLine.Length;
        var offset = 0;
        int? from = null;
        var to = 0;

        foreach (var segment in transcript.Segments)
        {
            var piece = segment.Text.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            var middle = segment.Start + ((segment.End - segment.Start) / 2);
            if (middle >= start && middle <= end)
            {
                from ??= offset;
                to = offset + piece.Length;
            }

            offset += piece.Length + separator;
        }

        return from is { } first ? new TextMark(first, to - first) : null;
    }
}
