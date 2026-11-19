using System.Text;

namespace Voicer.Core.Search;

/// <summary>
/// Turns what the user typed into a query of the full-text index (SQLite FTS5): words in quotes form a phrase, the
/// other words are looked up by their beginning, so other forms of a word match too (<c>contracts</c> finds
/// <c>contract</c>, <c>contracted</c>; the same works for the endings of Russian words); any of them is enough, the
/// passages with more of them rank higher. Only letters
/// and digits reach the index, so nothing typed is read as its syntax.
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
        var parts = text.Split('"');
        for (var i = 0; i < parts.Length; i++)
        {
            var words = Words(parts[i]);
            if (words.Count == 0)
            {
                continue;
            }

            // Odd parts were between quotes: a phrase, word for word.
            if (i % 2 == 1)
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
    /// Keeps the beginning of a word that its forms share: the end of a long word is mostly its ending.
    /// </summary>
    /// <param name="word">The word, in lower case.</param>
    /// <returns>The beginning to look up.</returns>
    internal static string Stem(string word)
    {
        return word.Length switch
        {
            >= 7 => word[..^2],
            >= 5 => word[..^1],
            _ => word,
        };
    }

    /// <summary>
    /// Takes the words of a text: runs of letters and digits, in lower case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The words.</returns>
    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var character in text + " ")
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(char.ToLowerInvariant(character));
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
