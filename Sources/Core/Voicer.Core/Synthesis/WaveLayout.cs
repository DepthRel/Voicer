namespace Voicer.Core.Synthesis;

/// <summary>
/// Where the sound of a WAV file is and what it is.
/// </summary>
/// <param name="Format">The format of the samples.</param>
/// <param name="DataOffset">Where the samples start in the file, in bytes.</param>
/// <param name="DataLength">How many bytes of samples the file holds.</param>
public sealed record WaveLayout(WaveFormat Format, long DataOffset, long DataLength)
{
    /// <summary>
    /// Gets how long the sound lasts.
    /// </summary>
    public TimeSpan Duration => Format.DurationOf(DataLength);
}
