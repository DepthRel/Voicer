using System.Globalization;
using System.Text;

namespace Voicer.Core.Summaries;

/// <summary>
/// Cuts a long text into parts a language model can read at once: by lines, a line that is too long by sentences, a
/// sentence that is too long by words, a word that is too long between its characters.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// The characters a sentence ends with when a space follows: the full stop of the Latin script means a number or a
    /// shortening without one.
    /// </summary>
    private static readonly char[] _sentenceEnds = ['.', '!', '?', '…'];

    /// <summary>
    /// The characters a sentence ends with whatever follows: the stops of Chinese and Japanese (full width and half
    /// width), the danda of the scripts of India, the question mark and the full stop of Arabic and Urdu, the full stop
    /// of Ethiopic. These scripts often put no space after them.
    /// </summary>
    private static readonly char[] _closedSentenceEnds = ['。', '！', '？', '．', '｡', '।', '॥', '؟', '۔', '።'];

    /// <summary>
    /// Cuts a text into parts.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxChars">The largest part, in characters; at least one.</param>
    /// <returns>
    /// The parts, in their order, none longer than <paramref name="maxChars"/> (unless a single character is longer:
    /// a pair of surrogates or a letter with its marks is never cut) and none empty.
    /// </returns>
    public static IReadOnlyList<string> Split(string text, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 1);

        var pieces = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(line => Fit(line, maxChars, 0));

        var parts = new List<string>();
        var part = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (part.Length > 0 && part.Length + 1 + piece.Length > maxChars)
            {
                parts.Add(part.ToString());
                part.Clear();
            }

            if (part.Length > 0)
            {
                part.Append('\n');
            }

            part.Append(piece);
        }

        if (part.Length > 0)
        {
            parts.Add(part.ToString());
        }

        return parts;
    }

    /// <summary>
    /// Cuts a piece that is too long into smaller ones: by sentences first, then by words, then between characters.
    /// </summary>
    /// <param name="piece">The piece.</param>
    /// <param name="maxChars">The largest piece.</param>
    /// <param name="level">0 for sentences, 1 for words, 2 for characters.</param>
    /// <returns>The pieces, each fitting.</returns>
    private static IEnumerable<string> Fit(string piece, int maxChars, int level)
    {
        if (piece.Length <= maxChars)
        {
            return [piece];
        }

        if (level == 2)
        {
            return Characters(piece, maxChars);
        }

        IEnumerable<string> bits = level == 0 ? Sentences(piece) : piece.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var separator = level == 0 ? string.Empty : " ";

        var result = new List<string>();
        var current = new StringBuilder();
        foreach (var bit in bits.SelectMany(b => Fit(b, maxChars, level + 1)))
        {
            if (current.Length > 0 && current.Length + separator.Length + bit.Length > maxChars)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(separator);
            }

            current.Append(bit);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result.Where(r => r.Length > 0);
    }

    /// <summary>
    /// Cuts a piece without spaces into runs of whole characters as the reader sees them: a pair of surrogates or a
    /// letter with its marks stays in one run.
    /// </summary>
    /// <param name="piece">The piece.</param>
    /// <param name="maxChars">The largest run.</param>
    /// <returns>The runs, in their order.</returns>
    private static List<string> Characters(string piece, int maxChars)
    {
        var runs = new List<string>();
        var start = 0;
        var end = 0;
        while (end < piece.Length)
        {
            var next = end + StringInfo.GetNextTextElementLength(piece, end);
            if (next - start > maxChars && end > start)
            {
                runs.Add(piece[start..end]);
                start = end;
            }

            end = next;
        }

        if (start < piece.Length)
        {
            runs.Add(piece[start..]);
        }

        return runs;
    }

    /// <summary>
    /// Splits a text after the ends of its sentences, keeping the spaces with the next sentence.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The sentences.</returns>
    private static List<string> Sentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var spaced = Array.IndexOf(_sentenceEnds, text[i]) >= 0 && (i + 1 == text.Length || text[i + 1] == ' ');
            if (spaced || Array.IndexOf(_closedSentenceEnds, text[i]) >= 0)
            {
                sentences.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            sentences.Add(text[start..]);
        }

        return sentences;
    }
}
