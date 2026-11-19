using Voicer.Localization;

namespace Voicer.Core.Transcripts;

/// <summary>
/// Names the languages of speech for the user: a code that Whisper detects (<c>en</c>, <c>de</c>, <c>yue</c>) becomes
/// its name in the language of the localizer (<c>English</c>, <c>German</c>, <c>Cantonese</c>).
/// </summary>
public static class SpeechLanguageNames
{
    /// <summary>
    /// The beginning of the key of the name of a language in the packs; the code completes it.
    /// </summary>
    public const string KEY_PREFIX = "SpeechLanguage.";

    /// <summary>
    /// Gets the codes of every language Whisper recognizes, as it reports them; each one has its name in the packs.
    /// </summary>
    public static IReadOnlyList<string> Codes { get; } =
    [
        "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi",
        "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml",
        "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs",
        "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am",
        "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln",
        "ha", "ba", "jw", "su", "yue",
    ];

    /// <summary>
    /// The codes, for a quick check whether a language has a name.
    /// </summary>
    private static readonly HashSet<string> _known = new(Codes, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Names a language of speech.
    /// </summary>
    /// <param name="localizer">The localizer of the reader.</param>
    /// <param name="code">The code of the language.</param>
    /// <returns>The name of the language; the code itself for a language without a name, so nothing is lost.</returns>
    public static string Name(ILocalizer localizer, string code)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var trimmed = code.Trim();

        return _known.Contains(trimmed) ? localizer.Get(KEY_PREFIX + trimmed.ToLowerInvariant()) : trimmed;
    }
}
