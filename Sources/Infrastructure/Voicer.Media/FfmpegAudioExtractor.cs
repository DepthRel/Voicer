using System.Globalization;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media;

/// <summary>
/// <see cref="IAudioExtractor"/> that runs the FFmpeg program, without a shell, and follows its progress.
/// </summary>
public sealed class FfmpegAudioExtractor : IAudioExtractor
{
    /// <summary>
    /// The sample rate the speech engine expects, in hertz.
    /// </summary>
    public const int SAMPLE_RATE = 16000;

    /// <summary>
    /// The options of the output: the first sound track, mono, 16 kHz, 16-bit WAV.
    /// </summary>
    private static readonly string[] _outputOptions =
    [
        "-map", "0:a:0", "-vn", "-sn", "-dn",
        "-ac", "1", "-ar", SAMPLE_RATE.ToString(CultureInfo.InvariantCulture), "-c:a", "pcm_s16le",
        "-f", "wav",
    ];

    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioExtractor"/> class.
    /// </summary>
    /// <param name="locator">The locator of FFmpeg.</param>
    public FfmpegAudioExtractor(IFfmpegLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <inheritdoc />
    public Task<TimeSpan> ExtractAsync(string source, string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        return FfmpegConversion.RunAsync(_locator, source, destination, _outputOptions, progress, cancellationToken);
    }
}
