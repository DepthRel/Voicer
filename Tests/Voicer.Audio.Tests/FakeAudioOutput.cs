using Voicer.Core.Playback;

namespace Voicer.Audio.Tests;

/// <summary>
/// A sound device for the tests: it keeps what is queued and plays only as far as the test says.
/// </summary>
internal sealed class FakeAudioOutput : IAudioOutput
{
    /// <summary>
    /// How many buffers the device holds.
    /// </summary>
    private const int CAPACITY = 4;

    /// <summary>
    /// Gets the frames of every buffer queued since the last stop.
    /// </summary>
    public List<int> Queued { get; } = [];

    /// <summary>
    /// Gets or sets how many frames the device has played since the last stop.
    /// </summary>
    public long Played { get; set; }

    /// <summary>
    /// Gets the last volume set.
    /// </summary>
    public float LastVolume { get; private set; } = -1;

    /// <summary>
    /// Gets how many times the device was stopped.
    /// </summary>
    public int Stops { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the device was told to play.
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the device is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public int FreeBuffers => CAPACITY - Queued.Count + PlayedBuffers();

    /// <inheritdoc />
    public long PlayedFrames => Played;

    /// <inheritdoc />
    public bool IsDrained => Played >= Queued.Sum();

    /// <inheritdoc />
    public float Volume
    {
        set => LastVolume = value;
    }

    /// <inheritdoc />
    public void Queue(ReadOnlySpan<byte> samples)
    {
        Queued.Add(samples.Length / PcmFormat.BYTES_PER_FRAME);
    }

    /// <inheritdoc />
    public void Play()
    {
        IsPlaying = true;
    }

    /// <inheritdoc />
    public void Stop()
    {
        Stops++;
        Queued.Clear();
        Played = 0;
        IsPlaying = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IsDisposed = true;
    }

    /// <summary>
    /// Counts the buffers already played through.
    /// </summary>
    /// <returns>The number of played buffers.</returns>
    private int PlayedBuffers()
    {
        var count = 0;
        var frames = 0L;
        foreach (var buffer in Queued)
        {
            frames += buffer;
            if (frames > Played)
            {
                break;
            }

            count++;
        }

        return count;
    }
}
