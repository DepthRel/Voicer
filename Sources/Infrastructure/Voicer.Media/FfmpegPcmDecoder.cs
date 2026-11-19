using System.Diagnostics;
using System.Globalization;
using Voicer.Core.Playback;
using Voicer.Localization;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media;

/// <summary>
/// <see cref="IPcmDecoder"/> that runs FFmpeg, without a shell, and reads the samples from its standard output; the
/// speed is changed by its <c>atempo</c> filter, which keeps the pitch of the voice.
/// </summary>
public sealed class FfmpegPcmDecoder : IPcmDecoder
{
    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegPcmDecoder"/> class.
    /// </summary>
    /// <param name="locator">The locator of FFmpeg.</param>
    public FfmpegPcmDecoder(IFfmpegLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <inheritdoc />
    public Stream Open(string file, TimeSpan start, double tempo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        var ffmpeg = _locator.Locate() ??
            throw LocalizedError.Create(m => new FileNotFoundException(m), "Ffmpeg.Missing");
        var info = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        foreach (var argument in Arguments(Path.GetFullPath(file), start, tempo))
        {
            info.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = info };
        process.Start();

        return new FfmpegPcmStream(process);
    }

    /// <summary>
    /// Builds the arguments of FFmpeg.
    /// </summary>
    /// <param name="file">The full path of the sound file.</param>
    /// <param name="start">The moment to start from; a negative one is the beginning.</param>
    /// <param name="tempo">The speed; it is kept between the slowest and the fastest one.</param>
    /// <returns>The arguments, one by one.</returns>
    internal static IReadOnlyList<string> Arguments(string file, TimeSpan start, double tempo)
    {
        var seconds = Math.Max(start.TotalSeconds, 0);
        var speed = Math.Clamp(tempo, IAudioPlayer.MIN_SPEED, IAudioPlayer.MAX_SPEED);

        // -ss before -i seeks in the input; the full path never starts with a dash, so it is never read as an option.
        var arguments = new List<string>
        {
            "-nostdin", "-hide_banner", "-loglevel", "error",
            "-ss", seconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", file,
            "-map", "0:a:0", "-vn", "-sn", "-dn",
        };

        if (Math.Abs(speed - 1) > 1e-6)
        {
            arguments.AddRange(["-af", "atempo=" + speed.ToString("0.######", CultureInfo.InvariantCulture)]);
        }

        arguments.AddRange(
        [
            "-ac", PcmFormat.CHANNELS.ToString(CultureInfo.InvariantCulture),
            "-ar", PcmFormat.SAMPLE_RATE.ToString(CultureInfo.InvariantCulture),
            "-f", "s16le", "pipe:1",
        ]);

        return arguments;
    }
}
