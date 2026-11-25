using System.Text;

namespace Voicer.Core.Search;

/// <summary>
/// Turns what the user typed into a query of the full-text index (SQLite FTS5): words in quotes form a phrase, the
/// other words are looked up by their beginning, so other forms of a word match too (<c>contracts</c> finds
/// <c>contract</c>, <c>contracted</c>; the same works for the endings of words of other languages); any of them is
/// enough, the passages with more of them rank higher. A number is looked up whole. Only the characters of words
/// (<see cref="WordCharacters"/>) reach the index, so nothing typed is read as its syntax.
/// </summary>
public static class FullTextQuery
{
    /// <summary>
    /// Builds the query.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <returns>The query, or <see langword="null"/> when there is no word to look for.</returns>
    public static string? Build(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var terms = new List<string>();
        foreach (var (words, isPhrase) in Parts(text))
        {
            if (isPhrase)
            {
                terms.Add("\"" + string.Join(' ', words) + "\"");
            }
            else
            {
                terms.AddRange(words.Select(word => "\"" + Stem(word) + "\"*"));
            }
        }

        return terms.Count == 0 ? null : string.Join(" OR ", terms.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// Takes the beginnings a word of a found text starts with when it matches what the user typed: the beginnings of
    /// the words looked up by their forms and the whole words of the phrases.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <returns>The beginnings, in lower case, without repeats; empty when there is no word.</returns>
    public static IReadOnlyList<string> Prefixes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return [.. Parts(text)
            .SelectMany(part => part.IsPhrase ? part.Words : part.Words.Select(Stem))
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Splits what the user typed into the parts between quotes (phrases) and the parts outside them.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <returns>The words of every part that has any, and whether the part is a phrase.</returns>
    private static IEnumerable<(List<string> Words, bool IsPhrase)> Parts(string text)
    {
        var parts = text.Split('"');
        for (var i = 0; i < parts.Length; i++)
        {
            var words = Words(parts[i]);

            // Odd parts were between quotes: a phrase, word for word.
            if (words.Count > 0)
            {
                yield return (words, i % 2 == 1);
            }
        }
    }

    /// <summary>
    /// Keeps the beginning of a word that its forms share: the end of a long word is mostly its ending. A word with a
    /// digit is a number or a code and has no forms: it stays whole. The cut is counted in whole characters, so a pair
    /// of surrogates is never split.
    /// </summary>
    /// <param name="word">The word, in lower case.</param>
    /// <returns>The beginning to look up.</returns>
    internal static string Stem(string word)
    {
        if (word.Any(char.IsDigit))
        {
            return word;
        }

        var characters = word.EnumerateRunes().ToList();
        var cut = characters.Count switch
        {
            >= 7 => 2,
            >= 5 => 1,
            _ => 0,
        };

        return string.Concat(characters.Take(characters.Count - cut));
    }

    /// <summary>
    /// Takes the words of a text: runs of the characters of words (<see cref="WordCharacters"/>), in lower case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The words.</returns>
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var character in (text + " ").EnumerateRunes())
        {
            if (WordCharacters.Contains(character))
            {
                word.Append(Rune.ToLowerInvariant(character).ToString());
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        return words;
    }
}
