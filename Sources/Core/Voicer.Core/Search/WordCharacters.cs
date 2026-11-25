using System.Globalization;
using System.Text;

namespace Voicer.Core.Search;

/// <summary>
/// Tells the characters of words from the characters between them, the way the full-text index of the history reads
/// a text: its tokenizer keeps letters, numbers, marks joined to letters and characters of private use
/// (<c>categories 'L* N* Co M*'</c>).
/// </summary>
internal static class WordCharacters
{
    /// <summary>
    /// Tells whether a character belongs to a word.
    /// </summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> for a letter, a number, a mark or a character of private use.</returns>
    internal static bool Contains(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.PrivateUse;
    }
}
