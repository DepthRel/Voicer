using FluentAssertions;
using NSubstitute;
using Voicer.Core.Enums;
using Voicer.Core.Playback;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the player of the window of a record: its buttons, its slider, its speeds and its volume.
/// </summary>
public sealed class PlayerViewModelTests
{
    /// <summary>
    /// The player of the sound.
    /// </summary>
    private readonly IAudioPlayer _player = Substitute.For<IAudioPlayer>();

    /// <summary>
    /// The view model under test, in Russian.
    /// </summary>
    private readonly PlayerViewModel _view;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerViewModelTests"/> class.
    /// </summary>
    public PlayerViewModelTests()
    {
        _player.Duration.Returns(TimeSpan.FromMinutes(45));
        _player.Speed.Returns(1);
        _player.Volume.Returns(1);
        _view = new PlayerViewModel(TestTexts.Russian, _player);
    }

    /// <summary>
    /// The speeds go from twice as slow to twice as fast, worded short in the numbers of the language; the normal one is
    /// chosen.
    /// </summary>
    [Fact]
    public void Speeds_GoFromTheSlowestToTheFastest()
    {
        _view.Speeds.Select(s => s.Factor).Should().Equal(0.5, 0.75, 1, 1.5, 2);
        _view.Speeds.Select(s => s.Label).Should().Equal(
            TestTexts.Russian.Format("Player.Speed.Factor", 0.5),
            TestTexts.Russian.Format("Player.Speed.Factor", 0.75),
            TestTexts.Russian.Format("Player.Speed.Factor", 1),
            TestTexts.Russian.Format("Player.Speed.Factor", 1.5),
            TestTexts.Russian.Format("Player.Speed.Factor", 2));
        _view.SelectedSpeed.Factor.Should().Be(1);
        _view.Volume.Should().Be(100);
        _view.IsMuted.Should().BeFalse();
    }

    /// <summary>
    /// The button turns the sound off, then back on with the volume it had.
    /// </summary>
    [Fact]
    public void ToggleMute_TurnsTheSoundOffAndBackWithItsVolume()
    {
        _view.Volume = 40;

        _view.ToggleMuteCommand.Execute(null);
        _view.Volume.Should().Be(0);
        _view.IsMuted.Should().BeTrue();
        _player.Received(1).Volume = 0;

        _view.ToggleMuteCommand.Execute(null);
        _view.Volume.Should().Be(40);
        _view.IsMuted.Should().BeFalse();
        _player.Received(2).Volume = 0.4;
    }

    /// <summary>
    /// The slider at zero turns the sound off, and away from zero turns it on; the button then brings back full volume
    /// when it had turned nothing off itself.
    /// </summary>
    [Fact]
    public void Slider_AtZeroIsTheSoundOff()
    {
        _view.Volume = 0;
        _view.IsMuted.Should().BeTrue();

        _view.Volume = 15;
        _view.IsMuted.Should().BeFalse();

        _view.Volume = 0;
        _view.ToggleMuteCommand.Execute(null);
        _view.Volume.Should().Be(100);
    }

    /// <summary>
    /// The button plays a paused sound and pauses a playing one.
    /// </summary>
    [Fact]
    public void TogglePlay_PlaysAndPauses()
    {
        _player.State.Returns(PlaybackState.Paused);
        _view.TogglePlayCommand.Execute(null);
        _player.Received(1).Play();

        _player.State.Returns(PlaybackState.Playing);
        _view.TogglePlayCommand.Execute(null);
        _player.Received(1).Pause();
    }

    /// <summary>
    /// The buttons jump ten seconds back and forward from where the sound is.
    /// </summary>
    [Fact]
    public void BackAndForward_JumpTenSeconds()
    {
        _player.Position.Returns(TimeSpan.FromSeconds(30));

        _view.BackCommand.Execute(null);
        _view.ForwardCommand.Execute(null);

        _player.Received(1).Seek(TimeSpan.FromSeconds(20));
        _player.Received(1).Seek(TimeSpan.FromSeconds(40));
    }

    /// <summary>
    /// A position chosen on the slider is applied once, on the next refresh, and then followed.
    /// </summary>
    [Fact]
    public void Slider_SeeksOnTheNextRefresh()
    {
        _view.PositionSeconds = 100;
        _view.PositionSeconds = 120;
        _player.DidNotReceiveWithAnyArgs().Seek(default);

        _player.Position.Returns(TimeSpan.FromSeconds(120));
        _view.Refresh();
        _view.Refresh();

        _player.Received(1).Seek(TimeSpan.FromSeconds(120));
        _view.TimeText.Should().Be(TestTexts.Russian.Format("Player.Time", "2:00", "45:00"));
    }

    /// <summary>
    /// The chosen speed and volume reach the player.
    /// </summary>
    [Fact]
    public void SpeedAndVolume_ReachThePlayer()
    {
        _view.SelectedSpeed = _view.Speeds[3];
        _view.Volume = 40;

        _player.Received(1).Speed = 1.5;
        _player.Received(1).Volume = 0.4;
    }

    /// <summary>
    /// A playback that stopped by itself shows why.
    /// </summary>
    [Fact]
    public void Refresh_ShowsAFailure()
    {
        _player.Failure.Returns(new IOException("device lost"));

        _view.Refresh();

        _view.Error.Should().Be(TestTexts.Russian.Format("Player.Failed", "device lost"));
        _view.IsPlaying.Should().BeFalse();
    }

    /// <summary>
    /// A pause asked by the window stops the sound and shows it stopped.
    /// </summary>
    [Fact]
    public void Pause_PausesThePlayer()
    {
        _player.State.Returns(PlaybackState.Playing);
        _view.Refresh();
        _player.When(p => p.Pause()).Do(_ => _player.State.Returns(PlaybackState.Paused));

        _view.Pause();

        _player.Received(1).Pause();
        _view.IsPlaying.Should().BeFalse();
    }
}
