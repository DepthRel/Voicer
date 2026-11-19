using System.Globalization;
using System.Text.RegularExpressions;

namespace Voicer.Media.Ffmpeg;

/// <summary>
/// Reads what FFmpeg reports: the duration of the input from its log and the position of the output from the lines of
/// <c>-progress</c>.
/// </summary>
public static partial class FfmpegOutputParser
{
    /// <summary>
    /// The key of the position of the output in the lines of <c>-progress</c>, in microseconds.
    /// </summary>
    private const string OUT_TIME_KEY = "out_time_us=";

    /// <summary>
    /// Reads the duration of the input from a line of the log, such as <c>  Duration: 00:01:23.45, start: 0.000000</c>.
    /// </summary>
    /// <param name="line">The line of the log.</param>
    /// <param name="duration">The duration, when the line gives one.</param>
    /// <returns><see langword="true"/> when the line gives a known duration.</returns>
    public static bool TryParseDuration(string? line, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (line is null)
        {
            return false;
        }

        var match = DurationPattern().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        var seconds = double.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture);
        duration = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

        return duration > TimeSpan.Zero;
    }

    /// <summary>
    /// Reads the position of the output from a line of <c>-progress</c>, such as <c>out_time_us=83450000</c>.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="position">The position, when the line gives one.</param>
    /// <returns><see langword="true"/> when the line gives a known position.</returns>
    public static bool TryParsePosition(string? line, out TimeSpan position)
    {
        position = TimeSpan.Zero;
        if (line is null || !line.StartsWith(OUT_TIME_KEY, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(line.AsSpan(OUT_TIME_KEY.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) ||
            microseconds < 0)
        {
            return false;
        }

        position = TimeSpan.FromMicroseconds(microseconds);

        return true;
    }

    /// <summary>
    /// Tells whether a line of the log says that the input has no sound to extract.
    /// </summary>
    /// <param name="line">The line of the log.</param>
    /// <returns><see langword="true"/> when the output got no stream.</returns>
    public static bool IsNoAudio(string? line)
    {
        return line is not null &&
            (line.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("matches no streams", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The duration in the log of FFmpeg.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"Duration:\s*(?<h>\d+):(?<m>\d{2}):(?<s>\d{2}(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();
}
