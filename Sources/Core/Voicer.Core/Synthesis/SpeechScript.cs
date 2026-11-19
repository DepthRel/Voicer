using Voicer.Core.Summaries;

namespace Voicer.Core.Synthesis;

/// <summary>
/// Cuts the text to speak into pieces a voice model speaks well at once. A model speaks a limited length per run and
/// keeps its intonation better on short pieces, so a long script is spoken sentence group by sentence group: every line
/// is a paragraph, a paragraph is cut at the ends of its sentences.
/// </summary>
public static class SpeechScript
{
    /// <summary>
    /// The longest piece, in characters: about fifteen seconds of speech.
    /// </summary>
    public const int MAX_PART_CHARS = 250;

    /// <summary>
    /// Cuts a text into pieces.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="maxChars">The longest piece, in characters.</param>
    /// <returns>The pieces in their order, none empty; none at all for a text without words.</returns>
    public static IReadOnlyList<ScriptPart> Split(string text, int maxChars = MAX_PART_CHARS)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 1);

        var paragraphs = text.ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(OneLine)
            .Where(p => p.Length > 0);

        var parts = new List<ScriptPart>();
        foreach (var paragraph in paragraphs)
        {
            var pieces = TextChunker.Split(paragraph, maxChars).Select(OneLine).Where(p => p.Length > 0).ToList();
            for (var i = 0; i < pieces.Count; i++)
            {
                parts.Add(new ScriptPart(pieces[i], i == pieces.Count - 1));
            }
        }

        return parts;
    }

    /// <summary>
    /// Puts a piece of text on one line with single spaces.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The words of the text separated by single spaces.</returns>
    private static string OneLine(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
