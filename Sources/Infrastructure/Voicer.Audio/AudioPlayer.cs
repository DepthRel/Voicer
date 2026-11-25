using System.ComponentModel;
using Voicer.Core.Enums;
using Voicer.Core.Playback;
using Voicer.Media;

namespace Voicer.Audio;

/// <summary>
/// <see cref="IAudioPlayer"/> over a sound file: a decoder turns the file into samples from the chosen moment at the
/// chosen speed, and a loop in the background feeds them to the sound device.
/// </summary>
/// <remarks>
/// The public members only change the wanted state under a lock and return at once. The loop alone touches the
/// decoder and the device: each run of the decoder carries the generation it was started for, so a jump or a new speed
/// asked meanwhile simply starts a new run, and stale progress of an old run is never shown.
/// </remarks>
public sealed class AudioPlayer : IAudioPlayer
{
    /// <summary>
    /// How many frames one buffer of the device holds: a tenth of a second.
    /// </summary>
    private const int BUFFER_FRAMES = PcmFormat.SAMPLE_RATE / 10;

    /// <summary>
    /// How long a pass of the loop waits before the next one.
    /// </summary>
    private static readonly TimeSpan _pumpInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// How long disposing waits for the loop to finish.
    /// </summary>
    private static readonly TimeSpan _stopTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The sound file.
    /// </summary>
    private readonly string _file;

    /// <summary>
    /// Decodes the file.
    /// </summary>
    private readonly IPcmDecoder _decoder;

    /// <summary>
    /// Opens the sound device the first time it is needed.
    /// </summary>
    private readonly Func<IAudioOutput> _createOutput;

    /// <summary>
    /// The clock of the loop.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Whether the loop runs by itself; the tests drive it pass by pass instead.
    /// </summary>
    private readonly bool _ownLoop;

    /// <summary>
    /// Guards the wanted state below.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// The samples read from the decoder for one buffer.
    /// </summary>
    private readonly byte[] _buffer = new byte[BUFFER_FRAMES * PcmFormat.BYTES_PER_FRAME];

    /// <summary>
    /// Stops the loop.
    /// </summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>
    /// The loop in the background, once started.
    /// </summary>
    private Task? _loop;

    /// <summary>
    /// What the player is doing.
    /// </summary>
    private PlaybackState _state;

    /// <summary>
    /// The moment of the sound where the current run starts; while paused, the position itself.
    /// </summary>
    private TimeSpan _anchor;

    /// <summary>
    /// The speed asked for.
    /// </summary>
    private double _speed = 1;

    /// <summary>
    /// The speed of the current run.
    /// </summary>
    private double _runSpeed = 1;

    /// <summary>
    /// The frames the device has played in the current run.
    /// </summary>
    private long _playedFrames;

    /// <summary>
    /// Grows with every change that needs a new run of the decoder.
    /// </summary>
    private long _generation;

    /// <summary>
    /// The volume asked for.
    /// </summary>
    private double _volume = 1;

    /// <summary>
    /// Whether the volume changed since the loop last set it.
    /// </summary>
    private bool _volumeChanged = true;

    /// <summary>
    /// Why the playback stopped by itself, or <see langword="null"/>.
    /// </summary>
    private Exception? _failure;

    /// <summary>
    /// The sound device, opened by the loop.
    /// </summary>
    private IAudioOutput? _output;

    /// <summary>
    /// The samples of the current run of the decoder, or <see langword="null"/>.
    /// </summary>
    private Stream? _samples;

    /// <summary>
    /// The generation the current run of the decoder was started for; <c>-1</c> without a run.
    /// </summary>
    private long _runGeneration = -1;

    /// <summary>
    /// Whether the decoder of the current run has given all its samples.
    /// </summary>
    private bool _endOfSamples;

    /// <summary>
    /// Whether the player is disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPlayer"/> class.
    /// </summary>
    /// <param name="file">The sound file.</param>
    /// <param name="duration">How long the sound lasts.</param>
    /// <param name="decoder">Decodes the file.</param>
    /// <param name="createOutput">Opens the sound device; it is called the first time the sound is played.</param>
    /// <param name="time">The clock of the loop; the system clock by default.</param>
    public AudioPlayer(string file, TimeSpan duration, IPcmDecoder decoder, Func<IAudioOutput> createOutput, TimeProvider? time = null)
        : this(file, duration, decoder, createOutput, time, ownLoop: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPlayer"/> class.
    /// </summary>
    /// <param name="file">The sound file.</param>
    /// <param name="duration">How long the sound lasts.</param>
    /// <param name="decoder">Decodes the file.</param>
    /// <param name="createOutput">Opens the sound device.</param>
    /// <param name="time">The clock of the loop.</param>
    /// <param name="ownLoop">Whether the loop runs by itself; <see langword="false"/> lets a test call each pass.</param>
    internal AudioPlayer(string file, TimeSpan duration, IPcmDecoder decoder, Func<IAudioOutput> createOutput, TimeProvider? time, bool ownLoop)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        _file = file;
        Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _createOutput = createOutput ?? throw new ArgumentNullException(nameof(createOutput));
        _time = time ?? TimeProvider.System;
        _ownLoop = ownLoop;
    }

    /// <inheritdoc />
    public TimeSpan Duration { get; }

    /// <inheritdoc />
    public TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                return CurrentPosition();
            }
        }
    }

    /// <inheritdoc />
    public PlaybackState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public Exception? Failure
    {
        get
        {
            lock (_gate)
            {
                return _failure;
            }
        }
    }

    /// <inheritdoc />
    public double Speed
    {
        get
        {
            lock (_gate)
            {
                return _speed;
            }
        }

        set
        {
            var speed = Math.Clamp(value, IAudioPlayer.MIN_SPEED, IAudioPlayer.MAX_SPEED);

            lock (_gate)
            {
                if (_state == PlaybackState.Playing)
                {
                    // The sound played so far counts at the old speed; the rest is decoded at the new one.
                    _anchor = CurrentPosition();
                    _speed = speed;
                    Restart();
                }
                else
                {
                    _speed = speed;
                }
            }
        }
    }

    /// <inheritdoc />
    public double Volume
    {
        get
        {
            lock (_gate)
            {
                return _volume;
            }
        }

        set
        {
            lock (_gate)
            {
                _volume = Math.Clamp(value, 0, 1);
                _volumeChanged = true;
            }
        }
    }

    /// <inheritdoc />
    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_state == PlaybackState.Playing)
            {
                return;
            }

            if (_state == PlaybackState.Ended || _anchor >= Duration)
            {
                _anchor = TimeSpan.Zero;
            }

            _failure = null;
            _state = PlaybackState.Playing;
            Restart();
        }

        if (_ownLoop)
        {
            _loop ??= Task.Run(LoopAsync);
        }
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (_gate)
        {
            if (_state != PlaybackState.Playing)
            {
                return;
            }

            _anchor = CurrentPosition();
            _state = PlaybackState.Paused;
            _generation++;
        }
    }

    /// <inheritdoc />
    public void Seek(TimeSpan position)
    {
        var target = position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;

        lock (_gate)
        {
            _anchor = target;
            if (_state == PlaybackState.Playing)
            {
                Restart();
            }
            else
            {
                _state = PlaybackState.Paused;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();
        var ended = true;
        try
        {
            ended = _loop?.Wait(_stopTimeout) ?? true;
        }
        catch (AggregateException)
        {
            // The loop ended with the cancellation; nothing is left to report.
        }

        if (ended)
        {
            Release();

            return;
        }

        // The loop is still inside the device or the decoder: they are released when it leaves them, never under it.
        _loop!.ContinueWith(_ => Release(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    /// <summary>
    /// Releases the decoder, the device and the stop of the loop, once the loop no longer uses them.
    /// </summary>
    private void Release()
    {
        CloseRun();
        _output?.Dispose();
        _stop.Dispose();
    }

    /// <summary>
    /// Does one pass of the loop: follows the wanted state, feeds the device and notes how far it has played.
    /// </summary>
    /// <param name="cancellationToken">Stops the reading of the decoder.</param>
    /// <returns>A task that completes with the pass.</returns>
    internal async Task PumpOnceAsync(CancellationToken cancellationToken)
    {
        PlaybackState state;
        long generation;
        TimeSpan anchor;
        double speed;
        bool volumeChanged;
        double volume;
        lock (_gate)
        {
            state = _state;
            generation = _generation;
            anchor = _anchor;
            speed = _runSpeed;
            volumeChanged = _volumeChanged;
            volume = _volume;
            _volumeChanged = false;
        }

        try
        {
            if (state != PlaybackState.Playing)
            {
                CloseRun();
                if (volumeChanged && _output is not null)
                {
                    _output.Volume = (float)volume;
                }

                return;
            }

            if (_output is null)
            {
                _output = _createOutput();
                volumeChanged = true;
            }

            if (volumeChanged)
            {
                _output.Volume = (float)volume;
            }

            if (generation != _runGeneration)
            {
                CloseRun();
                _samples = _decoder.Open(_file, anchor, speed);
                _runGeneration = generation;
                _endOfSamples = false;
            }

            await FeedAsync(generation, cancellationToken).ConfigureAwait(false);
            _output.Play();

            var played = _output.PlayedFrames;
            var finished = _endOfSamples && _output.IsDrained;
            lock (_gate)
            {
                if (_generation == generation && _state == PlaybackState.Playing)
                {
                    _playedFrames = played;
                    if (finished)
                    {
                        _state = PlaybackState.Ended;
                        _anchor = Duration;
                    }
                }
            }

            if (finished)
            {
                CloseRun();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or Win32Exception)
        {
            CloseRun();
            lock (_gate)
            {
                if (_generation == generation)
                {
                    _anchor = CurrentPosition();
                    _state = PlaybackState.Paused;
                    _failure = ex;
                }
            }
        }
    }

    /// <summary>
    /// Fills the free buffers of the device from the decoder, until the decoder ends or a new run is asked for.
    /// </summary>
    /// <param name="generation">The generation of the current run.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>A task that completes when the device is full or the decoder ended.</returns>
    private async Task FeedAsync(long generation, CancellationToken cancellationToken)
    {
        while (!_endOfSamples && _output!.FreeBuffers > 0)
        {
            var read = await ReadBufferAsync(cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                _endOfSamples = true;

                return;
            }

            _output.Queue(_buffer.AsSpan(0, read));

            lock (_gate)
            {
                if (_generation != generation)
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Reads one buffer of whole frames from the decoder.
    /// </summary>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>How many bytes were read; <c>0</c> at the end of the sound.</returns>
    private async Task<int> ReadBufferAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < _buffer.Length)
        {
            var read = await _samples!.ReadAsync(_buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total - (total % PcmFormat.BYTES_PER_FRAME);
    }

    /// <summary>
    /// Runs the passes until the player is disposed.
    /// </summary>
    /// <returns>A task that completes when the loop stops.</returns>
    private async Task LoopAsync()
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await PumpOnceAsync(token).ConfigureAwait(false);
                await Task.Delay(_pumpInterval, _time, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Asks the loop for a new run of the decoder from the anchor at the wanted speed; the caller holds the lock.
    /// </summary>
    private void Restart()
    {
        _generation++;
        _playedFrames = 0;
        _runSpeed = _speed;
    }

    /// <summary>
    /// Tells the position from the wanted state; the caller holds the lock.
    /// </summary>
    /// <returns>The moment of the sound being played now.</returns>
    private TimeSpan CurrentPosition()
    {
        if (_state == PlaybackState.Ended)
        {
            return Duration;
        }

        if (_state != PlaybackState.Playing)
        {
            return _anchor;
        }

        // The device plays the decoded sound, which runs faster or slower than the record by the speed.
        var played = TimeSpan.FromSeconds((double)_playedFrames / PcmFormat.SAMPLE_RATE * _runSpeed);
        var position = _anchor + played;

        return position > Duration ? Duration : position;
    }

    /// <summary>
    /// Ends the current run: stops the decoder and silences the device.
    /// </summary>
    private void CloseRun()
    {
        if (_runGeneration == -1 && _samples is null)
        {
            return;
        }

        _samples?.Dispose();
        _samples = null;
        _runGeneration = -1;
        _endOfSamples = false;
        _output?.Stop();
    }
}
