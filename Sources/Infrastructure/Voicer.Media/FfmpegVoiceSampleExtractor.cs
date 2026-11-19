using System.Globalization;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media;

/// <summary>
/// <see cref="IVoiceSampleExtractor"/> that runs FFmpeg, without a shell: the first thirty seconds of the first sound
/// track, mono, 24 kHz, 16-bit WAV. The voice models read no more than thirty seconds of a sample, and 24 kHz keeps the
/// timbre their speaker encoders listen to.
/// </summary>
public sealed class FfmpegVoiceSampleExtractor : IVoiceSampleExtractor
{
    /// <summary>
    /// The longest sample, in seconds.
    /// </summary>
    public const int MAX_SECONDS = 30;

    /// <summary>
    /// The sample rate of the sample, in hertz.
    /// </summary>
    public const int SAMPLE_RATE = 24000;

    /// <summary>
    /// The options of the output: the first sound track, its first seconds, mono, 24 kHz, 16-bit WAV.
    /// </summary>
    private static readonly string[] _outputOptions =
    [
        "-map", "0:a:0", "-vn", "-sn", "-dn", "-map_metadata", "-1",
        "-t", MAX_SECONDS.ToString(CultureInfo.InvariantCulture),
        "-ac", "1", "-ar", SAMPLE_RATE.ToString(CultureInfo.InvariantCulture), "-c:a", "pcm_s16le",
        "-f", "wav",
    ];

    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegVoiceSampleExtractor"/> class.
    /// </summary>
    /// <param name="locator">The locator of FFmpeg.</param>
    public FfmpegVoiceSampleExtractor(IFfmpegLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    /// <inheritdoc />
    public Task ExtractAsync(string source, string destination, CancellationToken cancellationToken)
    {
        return FfmpegConversion.RunAsync(_locator, source, destination, _outputOptions, null, cancellationToken);
    }
}
