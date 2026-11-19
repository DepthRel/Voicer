using Voicer.Core.Playback;
using Voicer.Localization;

namespace Voicer.Audio.OpenAl;

/// <summary>
/// <see cref="IAudioOutput"/> on the default sound device through OpenAL Soft: one source that plays a queue of a few
/// buffers, refilled as they are played.
/// </summary>
public sealed class OpenAlOutput : IAudioOutput
{
    /// <summary>
    /// How many buffers are in the queue at most: with 100 ms each, the sound is ahead of the decoder by at most 0.4 s.
    /// </summary>
    private const int BUFFER_COUNT = 4;

    /// <summary>
    /// The device.
    /// </summary>
    private readonly IntPtr _device;

    /// <summary>
    /// The context of the device.
    /// </summary>
    private readonly IntPtr _context;

    /// <summary>
    /// The source that plays the buffers.
    /// </summary>
    private readonly uint _source;

    /// <summary>
    /// Every buffer of the output.
    /// </summary>
    private readonly uint[] _buffers = new uint[BUFFER_COUNT];

    /// <summary>
    /// The buffers that can be filled.
    /// </summary>
    private readonly Stack<uint> _free = new();

    /// <summary>
    /// The buffers in the queue of the source, in their order, with the number of their frames.
    /// </summary>
    private readonly Queue<(uint Buffer, int Frames)> _queued = new();

    /// <summary>
    /// The frames of the buffers already played and taken back since the last stop.
    /// </summary>
    private long _completedFrames;

    /// <summary>
    /// Whether the output is disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAlOutput"/> class on the default sound device.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is no sound device, or OpenAL failed.</exception>
    /// <exception cref="DllNotFoundException">The OpenAL library is not found.</exception>
    public OpenAlOutput()
    {
        _device = OpenAlNative.OpenDevice(IntPtr.Zero);
        if (_device == IntPtr.Zero)
        {
            throw LocalizedError.Create(m => new InvalidOperationException(m), "Player.NoDevice");
        }

        _context = OpenAlNative.CreateContext(_device, IntPtr.Zero);
        if (_context == IntPtr.Zero || OpenAlNative.MakeContextCurrent(_context) == 0)
        {
            Release();
            throw LocalizedError.Create(m => new InvalidOperationException(m), "Player.NoDevice");
        }

        OpenAlNative.GenSources(1, out _source);
        OpenAlNative.GenBuffers(BUFFER_COUNT, _buffers);
        Check("creating the source and buffers");

        foreach (var buffer in _buffers)
        {
            _free.Push(buffer);
        }
    }

    /// <inheritdoc />
    public int FreeBuffers
    {
        get
        {
            Reclaim();

            return _free.Count;
        }
    }

    /// <inheritdoc />
    public long PlayedFrames
    {
        get
        {
            Reclaim();
            OpenAlNative.GetSourcei(_source, OpenAlNative.AL_SAMPLE_OFFSET, out var offset);

            return _completedFrames + Math.Max(offset, 0);
        }
    }

    /// <inheritdoc />
    public bool IsDrained
    {
        get
        {
            Reclaim();

            return _queued.Count == 0;
        }
    }

    /// <inheritdoc />
    public float Volume
    {
        set => OpenAlNative.Sourcef(_source, OpenAlNative.AL_GAIN, Math.Clamp(value, 0f, 1f));
    }

    /// <inheritdoc />
    public unsafe void Queue(ReadOnlySpan<byte> samples)
    {
        var length = samples.Length - (samples.Length % PcmFormat.BYTES_PER_FRAME);
        if (length == 0)
        {
            return;
        }

        // A source that ran dry has stopped, and a stopped source counts every queued buffer as played: what it played is
        // taken back, then it is rewound, so the new buffer waits to be played.
        OpenAlNative.GetSourcei(_source, OpenAlNative.AL_SOURCE_STATE, out var state);
        if (state == OpenAlNative.AL_STOPPED)
        {
            Reclaim();
            OpenAlNative.SourceRewind(_source);
        }

        if (!_free.TryPop(out var buffer))
        {
            throw new InvalidOperationException("No free buffer: check FreeBuffers before queueing.");
        }

        fixed (byte* data = samples)
        {
            OpenAlNative.BufferData(buffer, OpenAlNative.AL_FORMAT_MONO16, data, length, PcmFormat.SAMPLE_RATE);
        }

        OpenAlNative.SourceQueueBuffers(_source, 1, in buffer);
        Check("queueing a buffer");

        _queued.Enqueue((buffer, length / PcmFormat.BYTES_PER_FRAME));
    }

    /// <inheritdoc />
    public void Play()
    {
        // The played buffers are taken back first: a stopped source told to play would play them all over again.
        Reclaim();
        OpenAlNative.GetSourcei(_source, OpenAlNative.AL_SOURCE_STATE, out var state);
        if (state != OpenAlNative.AL_PLAYING && _queued.Count > 0)
        {
            OpenAlNative.SourcePlay(_source);
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        // A stopped source counts every queued buffer as played: all of them come back.
        OpenAlNative.SourceStop(_source);
        Reclaim();
        OpenAlNative.Sourcei(_source, OpenAlNative.AL_BUFFER, 0);
        OpenAlNative.SourceRewind(_source);
        _queued.Clear();
        _free.Clear();
        foreach (var buffer in _buffers)
        {
            _free.Push(buffer);
        }

        _completedFrames = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OpenAlNative.SourceStop(_source);
        OpenAlNative.Sourcei(_source, OpenAlNative.AL_BUFFER, 0);
        OpenAlNative.DeleteSources(1, in _source);
        OpenAlNative.DeleteBuffers(BUFFER_COUNT, _buffers);
        Release();
    }

    /// <summary>
    /// Takes the played buffers back from the source and counts their frames.
    /// </summary>
    private void Reclaim()
    {
        OpenAlNative.GetSourcei(_source, OpenAlNative.AL_BUFFERS_PROCESSED, out var processed);
        for (var i = 0; i < processed; i++)
        {
            OpenAlNative.SourceUnqueueBuffers(_source, 1, out var buffer);
            if (_queued.TryDequeue(out var played))
            {
                _completedFrames += played.Frames;
            }

            _free.Push(buffer);
        }
    }

    /// <summary>
    /// Releases the context and the device.
    /// </summary>
    private void Release()
    {
        OpenAlNative.MakeContextCurrent(IntPtr.Zero);
        if (_context != IntPtr.Zero)
        {
            OpenAlNative.DestroyContext(_context);
        }

        OpenAlNative.CloseDevice(_device);
    }

    /// <summary>
    /// Throws when the last call of OpenAL failed.
    /// </summary>
    /// <param name="operation">What was being done, for the developer.</param>
    /// <exception cref="InvalidOperationException">OpenAL reported an error.</exception>
    private static void Check(string operation)
    {
        var error = OpenAlNative.GetError();
        if (error != OpenAlNative.AL_NO_ERROR)
        {
            throw new InvalidOperationException($"OpenAL failed {operation}: error 0x{error:X}.");
        }
    }
}
