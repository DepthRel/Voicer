using System.Text;

namespace Voicer.Core.Summaries;

/// <summary>
/// Estimates how many tokens of a language model a text takes, and fits texts into a number of tokens.
/// </summary>
/// <remarks>
/// The estimate errs on the large side for every script: a letter of a script written in words of several letters
/// (Latin, Cyrillic, Greek, Arabic, Hebrew) is counted as <c>1 / 2.5</c> of a token; a sign of a script a tokenizer cuts
/// finer (Chinese, Japanese, Korean, the scripts of India and of South-East Asia, Georgian, Armenian, Ethiopic) and
/// anything outside the basic plane, as a whole token.
/// </remarks>
public static class TokenBudget
{
    /// <summary>
    /// How many characters of a script written in words a token holds at least: about 2.5 in Cyrillic, 4 in English.
    /// </summary>
    private const double CHARS_PER_TOKEN = 2.5;

    /// <summary>
    /// Estimates how many tokens a text takes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens, rounded up.</returns>
    public static int Estimate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = 0.0;
        foreach (var rune in text.EnumerateRunes())
        {
            tokens += IsDense(rune) ? 1 : 1 / CHARS_PER_TOKEN;
        }

        return (int)Math.Ceiling(tokens);
    }

    /// <summary>
    /// Cuts a text into parts of at most a number of tokens each, with the cuts of <see cref="TextChunker"/>: by lines,
    /// sentences, words.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The largest part, in tokens; at least one.</param>
    /// <returns>The parts, in their order, none empty.</returns>
    public static IReadOnlyList<string> Split(string text, int tokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(tokens, 1);

        var parts = new List<string>();
        foreach (var part in TextChunker.Split(text, CharactersFor(text, tokens)))
        {
            // The text may be denser in one place than on average: such a part is cut again by its own density, unless
            // it is a single character that cannot be cut.
            if (Estimate(part) <= tokens || part.Length >= text.Length)
            {
                parts.Add(part);
            }
            else
            {
                parts.AddRange(Split(part, tokens));
            }
        }

        return parts;
    }

    /// <summary>
    /// Gives the beginning of a text that fits into a number of tokens, cut between characters, never inside one.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">How many tokens fit; at least zero.</param>
    /// <returns>The text itself when it fits, otherwise its longest beginning that fits.</returns>
    public static string Cut(string text, int tokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);

        var used = 0.0;
        var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            used += IsDense(rune) ? 1 : 1 / CHARS_PER_TOKEN;
            if (used > tokens)
            {
                return text[..length];
            }

            length += rune.Utf16SequenceLength;
        }

        return text;
    }

    /// <summary>
    /// Tells how many characters of a text take about a number of tokens, by the average density of the text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The characters; at least one.</returns>
    private static int CharactersFor(string text, int tokens)
    {
        var estimate = Estimate(text);
        if (estimate == 0)
        {
            return Math.Max(1, text.Length);
        }

        return (int)Math.Max(1, (long)tokens * text.Length / estimate);
    }

    /// <summary>
    /// Tells whether a character belongs to a script a tokenizer cuts into about one token per sign.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> for such a script, or a character outside the basic plane.</returns>
    private static bool IsDense(Rune rune)
    {
        var value = rune.Value;

        return value > 0xFFFF ||
            value is >= 0x0530 and <= 0x058F ||
            value is >= 0x0900 and <= 0x109F ||
            value is >= 0x10A0 and <= 0x139F ||
            value is >= 0x1780 and <= 0x17FF ||
            value is >= 0x2E80 and <= 0xA4CF ||
            value is >= 0xAC00 and <= 0xD7AF ||
            value is >= 0xF900 and <= 0xFAFF ||
            value is >= 0xFF00 and <= 0xFFEF;
    }
}
