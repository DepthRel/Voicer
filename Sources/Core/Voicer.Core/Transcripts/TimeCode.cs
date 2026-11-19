namespace Voicer.Core.Transcripts;

/// <summary>
/// Writes a moment of a record as a clock: <c>4:05</c>, or <c>1:02:03</c> from an hour on.
/// </summary>
public static class TimeCode
{
    /// <summary>
    /// Writes a moment or a duration as a clock; a negative one is zero.
    /// </summary>
    /// <param name="value">The moment or the duration.</param>
    /// <returns>The clock.</returns>
    public static string Format(TimeSpan value)
    {
        var time = value < TimeSpan.Zero ? TimeSpan.Zero : value;

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
