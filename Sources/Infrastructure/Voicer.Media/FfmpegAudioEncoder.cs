using System.Globalization;
using Voicer.Core.Playback;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media;

/// <summary>
/// <see cref="IAudioEncoder"/> that compresses the sound with FFmpeg into Opus, mono, 48 kHz, 32 kbit/s: clear speech
/// at about 14 MB an hour.
/// </summary>
public sealed class FfmpegAudioEncoder : IAudioEncoder
{
    /// <summary>
    /// The extension of the files it writes.
    /// </summary>
    public const string EXTENSION = ".ogg";

    /// <summary>
    /// The bit rate of the compressed sound.
    /// </summary>
    private const string BIT_RATE = "32k";

    /// <summary>
    /// The options of the output: the first sound track, mono, 48 kHz, Opus in Ogg, without the pictures of a video.
    /// </summary>
    private static readonly string[] _outputOptions =
    [
        "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1",
        "-ac", PcmFormat.CHANNELS.ToString(CultureInfo.InvariantCulture),
        "-ar", PcmFormat.SAMPLE_RATE.ToString(CultureInfo.InvariantCulture),
        "-c:a", "libopus", "-b:a", BIT_RATE,
        "-f", "ogg",
    ];

    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioEncoder"/> class.
    /// </summary>
    /// <param name="locator">The locator of FFmpeg.</param>
    public FfmpegAudioEncoder(IFfmpegLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <inheritdoc />
    public Task<TimeSpan> EncodeAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return FfmpegConversion.RunAsync(_locator, source, destination, _outputOptions, progress, cancellationToken);
    }
}
