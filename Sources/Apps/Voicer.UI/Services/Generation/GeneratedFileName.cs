using System.Text;

namespace Voicer.UI.Services.Generation;

/// <summary>
/// Names the file of a generated speech after the first words of its text, so the history and the save dialog show
/// what it says.
/// </summary>
public static class GeneratedFileName
{
    /// <summary>
    /// The extension of a generated speech.
    /// </summary>
    public const string EXTENSION = ".wav";

    /// <summary>
    /// How many words of the text the name takes at most.
    /// </summary>
    private const int MAX_WORDS = 6;

    /// <summary>
    /// The longest name without the extension, in characters.
    /// </summary>
    private const int MAX_LENGTH = 60;

    /// <summary>
    /// The name of a text without a word that can be put in a file name.
    /// </summary>
    private const string FALLBACK = "speech";

    /// <summary>
    /// Names the file of a speech.
    /// </summary>
    /// <param name="text">The spoken text.</param>
    /// <returns>A file name with <see cref="EXTENSION"/>, safe on every system.</returns>
    public static string From(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean)
            .Where(w => w.Length > 0)
            .Take(MAX_WORDS);

        var name = string.Join(' ', words);
        if (name.Length > MAX_LENGTH)
        {
            name = name[..MAX_LENGTH].TrimEnd();
        }

        if (name.Length == 0)
        {
            return FALLBACK + EXTENSION;
        }

        // A single word that Windows keeps for a device (con, nul, com1…) cannot be a file name there.
        return (IsDeviceName(name) ? FALLBACK + " " + name : name) + EXTENSION;
    }

    /// <summary>
    /// Tells whether a name is one Windows keeps for a device.
    /// </summary>
    /// <param name="name">The name without its extension.</param>
    /// <returns><see langword="true"/> for <c>con</c>, <c>prn</c>, <c>aux</c>, <c>nul</c>, <c>com1</c>…<c>lpt9</c>.</returns>
    private static bool IsDeviceName(string name)
    {
        var upper = name.ToUpperInvariant();

        return upper is "CON" or "PRN" or "AUX" or "NUL" ||
            (upper.Length == 4 && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal)) && char.IsAsciiDigit(upper[3]));
    }

    /// <summary>
    /// Keeps the letters, digits, dashes and apostrophes of a word: nothing a file system refuses or reads specially.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The cleaned word, maybe empty.</returns>
    private static string Clean(string word)
    {
        var kept = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '\'')
            {
                kept.Append(c);
            }
        }

        return kept.ToString().Trim('-', '\'');
    }
}
