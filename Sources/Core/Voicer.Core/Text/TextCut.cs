namespace Voicer.Core.Text;

/// <summary>
/// Cuts texts to a length without breaking a character: a pair of surrogates (a sign outside the basic plane, such as
/// a rare ideograph or an emoji) is never split, so the text stays valid for UTF-8, JSON and SQLite.
/// </summary>
public static class TextCut
{
    /// <summary>
    /// Gives the beginning of a text of at most a number of UTF-16 units.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The longest beginning; at least zero.</param>
    /// <returns>The text itself when it fits; otherwise its beginning, one unit shorter when the cut would split a pair.</returns>
    public static string Beginning(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (text.Length <= maxLength)
        {
            return text;
        }

        var length = maxLength > 0 && char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;

        return text[..length];
    }
}
