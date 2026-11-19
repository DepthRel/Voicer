namespace Voicer.Core.Playback;

/// <summary>
/// The raw sound the decoder hands to the audio output: signed 16-bit little-endian samples, one channel, 48 kHz.
/// </summary>
public static class PcmFormat
{
    /// <summary>
    /// The number of samples per second.
    /// </summary>
    public const int SAMPLE_RATE = 48000;

    /// <summary>
    /// The number of channels.
    /// </summary>
    public const int CHANNELS = 1;

    /// <summary>
    /// The number of bytes of one sample of one channel.
    /// </summary>
    public const int BYTES_PER_SAMPLE = 2;

    /// <summary>
    /// The number of bytes of one frame: a sample of every channel.
    /// </summary>
    public const int BYTES_PER_FRAME = CHANNELS * BYTES_PER_SAMPLE;
}
