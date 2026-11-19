using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Voicer.Core.Enums;
using Voicer.Core.Playback;
using Voicer.Media;

namespace Voicer.Audio.Tests;

/// <summary>
/// Tests of the player: what it asks the decoder for, what it feeds the device, and where it says it is.
/// </summary>
public sealed class AudioPlayerTests : IDisposable
{
    /// <summary>
    /// The sound file of every test.
    /// </summary>
    private const string FILE = "record.ogg";

    /// <summary>
    /// How long the sound of every test lasts.
    /// </summary>
    private static readonly TimeSpan _duration = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The decoder: it gives one second of silence per run unless a test says otherwise.
    /// </summary>
    private readonly IPcmDecoder _decoder = Substitute.For<IPcmDecoder>();

    /// <summary>
    /// The sound device.
    /// </summary>
    private readonly FakeAudioOutput _output = new();

    /// <summary>
    /// The player under test; its loop is driven by the tests.
    /// </summary>
    private readonly AudioPlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioPlayerTests"/> class.
    /// </summary>
    public AudioPlayerTests()
    {
        _decoder.Open(default!, default, default).ReturnsForAnyArgs(_ => Seconds(1));
        _player = new AudioPlayer(FILE, _duration, _decoder, () => _output, TimeProvider.System, ownLoop: false);
    }

    /// <summary>
    /// Disposes the player.
    /// </summary>
    public void Dispose()
    {
        _player.Dispose();
    }

    /// <summary>
    /// Before playing, nothing is decoded and the device is not opened.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Paused_DecodesNothing()
    {
        await _player.PumpOnceAsync(CancellationToken.None);

        _decoder.DidNotReceiveWithAnyArgs().Open(default!, default, default);
        _player.State.Should().Be(PlaybackState.Paused);
        _player.Position.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// Playing decodes from the position at the speed of the record and fills the device.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Play_DecodesFromThePositionAndFillsTheDevice()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _decoder.Received(1).Open(FILE, TimeSpan.Zero, 1);
        _output.Queued.Should().HaveCount(4).And.OnlyContain(frames => frames == PcmFormat.SAMPLE_RATE / 10);
        _output.IsPlaying.Should().BeTrue();
        _player.State.Should().Be(PlaybackState.Playing);
    }

    /// <summary>
    /// The position follows what the device has played, faster at a higher speed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Position_FollowsTheDeviceAtTheSpeed()
    {
        _player.Speed = 2;
        _player.Seek(TimeSpan.FromSeconds(10));
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _output.Played = PcmFormat.SAMPLE_RATE / 4;
        await _player.PumpOnceAsync(CancellationToken.None);

        _decoder.Received(1).Open(FILE, TimeSpan.FromSeconds(10), 2);
        _player.Position.Should().Be(TimeSpan.FromSeconds(10.5));
    }

    /// <summary>
    /// A jump while playing starts the decoder again from the new moment; the device starts over.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Seek_WhilePlaying_DecodesFromTheNewMoment()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.Seek(TimeSpan.FromSeconds(30));
        _player.Position.Should().Be(TimeSpan.FromSeconds(30));
        await _player.PumpOnceAsync(CancellationToken.None);

        _decoder.Received(1).Open(FILE, TimeSpan.FromSeconds(30), 1);
        _output.Stops.Should().Be(1);
    }

    /// <summary>
    /// A jump stays between the beginning and the end.
    /// </summary>
    [Fact]
    public void Seek_StaysInsideTheSound()
    {
        _player.Seek(TimeSpan.FromSeconds(-10));
        _player.Position.Should().Be(TimeSpan.Zero);

        _player.Seek(TimeSpan.FromHours(1));
        _player.Position.Should().Be(_duration);
    }

    /// <summary>
    /// A pause keeps the position, silences the device, and playing goes on from there.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Pause_KeepsThePosition()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);
        _output.Played = PcmFormat.SAMPLE_RATE / 5;
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.Pause();
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.State.Should().Be(PlaybackState.Paused);
        _player.Position.Should().Be(TimeSpan.FromSeconds(0.2));
        _output.Stops.Should().Be(1);

        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);
        _decoder.Received(1).Open(FILE, TimeSpan.FromSeconds(0.2), 1);
    }

    /// <summary>
    /// A new speed while playing keeps the position and decodes the rest at the new speed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Speed_WhilePlaying_DecodesTheRestAtTheNewSpeed()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);
        _output.Played = PcmFormat.SAMPLE_RATE / 2;
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.Speed = 0.5;
        await _player.PumpOnceAsync(CancellationToken.None);

        _decoder.Received(1).Open(FILE, TimeSpan.FromSeconds(0.5), 0.5);
        _player.Position.Should().Be(TimeSpan.FromSeconds(0.5));
    }

    /// <summary>
    /// The speed stays between twice as slow and twice as fast.
    /// </summary>
    [Fact]
    public void Speed_StaysInItsRange()
    {
        _player.Speed = 10;
        _player.Speed.Should().Be(IAudioPlayer.MAX_SPEED);

        _player.Speed = 0.1;
        _player.Speed.Should().Be(IAudioPlayer.MIN_SPEED);
    }

    /// <summary>
    /// The volume reaches the device, kept between silent and as recorded.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Volume_ReachesTheDevice()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.Volume = 1.7;
        await _player.PumpOnceAsync(CancellationToken.None);
        _output.LastVolume.Should().Be(1);

        _player.Volume = 0.25;
        await _player.PumpOnceAsync(CancellationToken.None);
        _output.LastVolume.Should().Be(0.25f);
    }

    /// <summary>
    /// When the sound ends and the device has played it all, the player ends at the end; playing again starts over.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task End_StopsAtTheEndAndStartsOverOnPlay()
    {
        _decoder.Open(default!, default, default).ReturnsForAnyArgs(_ => Seconds(0.15));
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _output.Played = (long)(PcmFormat.SAMPLE_RATE * 0.15);
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.State.Should().Be(PlaybackState.Ended);
        _player.Position.Should().Be(_duration);

        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);
        _decoder.Received(2).Open(FILE, TimeSpan.Zero, 1);
    }

    /// <summary>
    /// A decoder that fails pauses the player and tells why.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task DecoderFailure_PausesWithTheReason()
    {
        var failure = new IOException("broken file");
        _decoder.Open(default!, default, default).ThrowsForAnyArgs(failure);

        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.State.Should().Be(PlaybackState.Paused);
        _player.Failure.Should().BeSameAs(failure);
    }

    /// <summary>
    /// A sound device that cannot be opened pauses the player and tells why.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task MissingDevice_PausesWithTheReason()
    {
        using var player = new AudioPlayer(FILE, _duration, _decoder, () => throw new InvalidOperationException("no device"), TimeProvider.System, ownLoop: false);

        player.Play();
        await player.PumpOnceAsync(CancellationToken.None);

        player.State.Should().Be(PlaybackState.Paused);
        player.Failure!.Message.Should().Be("no device");
    }

    /// <summary>
    /// Disposing the player closes the device.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Dispose_ClosesTheDevice()
    {
        _player.Play();
        await _player.PumpOnceAsync(CancellationToken.None);

        _player.Dispose();

        _output.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Makes a stream of silence.
    /// </summary>
    /// <param name="seconds">How long it lasts.</param>
    /// <returns>The samples.</returns>
    private static MemoryStream Seconds(double seconds)
    {
        return new MemoryStream(new byte[(int)(PcmFormat.SAMPLE_RATE * seconds) * PcmFormat.BYTES_PER_FRAME]);
    }
}
