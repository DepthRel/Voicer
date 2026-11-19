namespace Voicer.Core.Synthesis;

/// <summary>
/// The format of the samples of a WAV file: integer PCM or floating point, the channels, the rate and the bits.
/// </summary>
/// <param name="FormatTag">The format of the samples: <see cref="PCM"/> or <see cref="IEEE_FLOAT"/>.</param>
/// <param name="Channels">The number of channels.</param>
/// <param name="SampleRate">The samples per second of one channel.</param>
/// <param name="BitsPerSample">The bits of one sample of one channel.</param>
public sealed record WaveFormat(int FormatTag, int Channels, int SampleRate, int BitsPerSample)
{
    /// <summary>
    /// The format tag of integer samples.
    /// </summary>
    public const int PCM = 1;

    /// <summary>
    /// The format tag of floating-point samples.
    /// </summary>
    public const int IEEE_FLOAT = 3;

    /// <summary>
    /// Gets the bytes of one sample of every channel.
    /// </summary>
    public int BlockAlign => Channels * BitsPerSample / 8;

    /// <summary>
    /// Gets the bytes of one second of sound.
    /// </summary>
    public long BytesPerSecond => (long)SampleRate * BlockAlign;

    /// <summary>
    /// Tells how long a number of bytes of sound lasts.
    /// </summary>
    /// <param name="bytes">The bytes of sound.</param>
    /// <returns>The duration.</returns>
    public TimeSpan DurationOf(long bytes)
    {
        return TimeSpan.FromSeconds((double)bytes / BytesPerSecond);
    }

    /// <summary>
    /// Tells how many bytes of sound a duration takes, whole samples only.
    /// </summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The bytes, a multiple of <see cref="BlockAlign"/>.</returns>
    public long BytesOf(TimeSpan duration)
    {
        var samples = (long)Math.Round(duration.TotalSeconds * SampleRate);

        return samples * BlockAlign;
    }
}
