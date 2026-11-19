using Voicer.Core.Playback;

namespace Voicer.Audio;

/// <summary>
/// The sound device, fed with buffers of raw samples (<see cref="PcmFormat"/>) that it plays one after another. It is
/// used from one thread at a time.
/// </summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>
    /// Gets how many buffers can be queued now; the buffers already played are taken back first.
    /// </summary>
    int FreeBuffers { get; }

    /// <summary>
    /// Gets how many frames were played since the last <see cref="Stop"/>.
    /// </summary>
    long PlayedFrames { get; }

    /// <summary>
    /// Gets a value indicating whether everything queued has been played.
    /// </summary>
    bool IsDrained { get; }

    /// <summary>
    /// Sets the volume, from <c>0</c> (silent) to <c>1</c> (as recorded).
    /// </summary>
    float Volume { set; }

    /// <summary>
    /// Queues one buffer of samples after the others.
    /// </summary>
    /// <param name="samples">The samples, whole frames; at most one buffer is used per call.</param>
    void Queue(ReadOnlySpan<byte> samples);

    /// <summary>
    /// Plays what is queued; it also starts again after the queue ran dry.
    /// </summary>
    void Play();

    /// <summary>
    /// Stops and forgets everything queued; <see cref="PlayedFrames"/> starts from zero again.
    /// </summary>
    void Stop();
}
