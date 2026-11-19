using Voicer.Core.Transcripts;
using Voicer.Localization;

namespace Voicer.UI.ViewModels.Formatting;

/// <summary>
/// Words sizes, speeds, durations, moments and languages for the user, with the units of the language in use.
/// </summary>
public static class DisplayText
{
    /// <summary>
    /// The number of bytes in a kibibyte.
    /// </summary>
    private const double KIB = 1024;

    /// <summary>
    /// Words a size: <c>2.9 GB</c>, <c>512 MB</c>, <c>800 KB</c>, <c>12 B</c> (binary units, as the systems show memory).
    /// </summary>
    /// <param name="localizer">The localizer of the units.</param>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The size.</returns>
    public static string Bytes(ILocalizer localizer, double bytes)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var size = Math.Max(bytes, 0);
        if (size >= KIB * KIB * KIB)
        {
            return localizer.Format("Units.Gigabytes", size / (KIB * KIB * KIB));
        }

        if (size >= KIB * KIB)
        {
            return localizer.Format("Units.Megabytes", size / (KIB * KIB));
        }

        return size >= KIB
            ? localizer.Format("Units.Kilobytes", size / KIB)
            : localizer.Format("Units.Bytes", size);
    }

    /// <summary>
    /// Words a speed: <c>25.3 MB/s</c>.
    /// </summary>
    /// <param name="localizer">The localizer of the units.</param>
    /// <param name="bytesPerSecond">The speed in bytes per second.</param>
    /// <returns>The speed.</returns>
    public static string Speed(ILocalizer localizer, double bytesPerSecond)
    {
        return localizer.Format("Units.PerSecond", Bytes(localizer, bytesPerSecond));
    }

    /// <summary>
    /// The mark of a value that is not known.
    /// </summary>
    private const string UNKNOWN = "—";

    /// <summary>
    /// Words a moment in the short date and time of the language in use, on the clock it was taken by.
    /// </summary>
    /// <param name="localizer">The localizer whose culture words the moment.</param>
    /// <param name="moment">The moment.</param>
    /// <returns>The date and time.</returns>
    public static string Moment(ILocalizer localizer, DateTimeOffset moment)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return moment.ToString("g", localizer.Culture);
    }

    /// <summary>
    /// Words the detected language of a speech: its full name, or a dash when it is not known.
    /// </summary>
    /// <param name="localizer">The localizer whose language names the language.</param>
    /// <param name="language">The code of the language, or <see langword="null"/>.</param>
    /// <returns>The language.</returns>
    public static string Language(ILocalizer localizer, string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? UNKNOWN : SpeechLanguageNames.Name(localizer, language);
    }

    /// <summary>
    /// Words a duration as a clock: <c>4:05</c>, or <c>1:02:03</c> from an hour on.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The duration.</returns>
    public static string Duration(TimeSpan duration)
    {
        return TimeCode.Format(duration);
    }
}
