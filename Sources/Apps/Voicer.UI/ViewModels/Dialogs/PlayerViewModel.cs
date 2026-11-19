using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Core.Enums;
using Voicer.Core.Playback;
using Voicer.Localization;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// The player of the window of a record: play and pause, ten seconds back and forward, the position, the volume and the
/// speed. The window calls <see cref="Refresh"/> a few times a second to follow the sound.
/// </summary>
/// <remarks>
/// A position chosen on the slider is applied on the next refresh, so dragging the slider does not restart the decoder
/// for every pixel.
/// </remarks>
public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// How far the buttons jump back and forward.
    /// </summary>
    private static readonly TimeSpan _jump = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The speeds to choose from, from the slowest to the fastest.
    /// </summary>
    private static readonly double[] _speedFactors = [0.5, 0.75, 1, 1.5, 2];

    /// <summary>
    /// The volume the sound comes back with when it is turned on and no other was turned off, from 0 to 100.
    /// </summary>
    private const double FULL_VOLUME = 100;

    /// <summary>
    /// The localizer of the texts.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// The player of the sound.
    /// </summary>
    private readonly IAudioPlayer _player;

    /// <summary>
    /// The position shown, in seconds.
    /// </summary>
    private double _positionSeconds;

    /// <summary>
    /// The position chosen on the slider and not applied yet, in seconds.
    /// </summary>
    private double? _pendingSeek;

    /// <summary>
    /// The volume the sound had when the button turned it off, from 0 to 100; it comes back when the button turns the
    /// sound on.
    /// </summary>
    private double _volumeBeforeMute = FULL_VOLUME;

    /// <summary>
    /// Whether the sound plays.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Why the sound stopped by itself, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>
    /// The chosen speed.
    /// </summary>
    [ObservableProperty]
    private SpeedOptionViewModel _selectedSpeed;

    /// <summary>
    /// The volume, from 0 to 100.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMuted))]
    private double _volume;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="player">The player of the sound; it is disposed with this view model.</param>
    public PlayerViewModel(ILocalizer localizer, IAudioPlayer player)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        _player = player ?? throw new ArgumentNullException(nameof(player));

        Speeds = [.. _speedFactors.Select(factor => new SpeedOptionViewModel(factor, localizer.Format("Player.Speed.Factor", factor)))];
        _selectedSpeed = Speeds.MinBy(s => Math.Abs(s.Factor - player.Speed))!;
        _volume = Math.Round(player.Volume * 100);
        _positionSeconds = player.Position.TotalSeconds;
    }

    /// <summary>
    /// Gets the speeds to choose from, from the slowest to the fastest.
    /// </summary>
    public IReadOnlyList<SpeedOptionViewModel> Speeds { get; }

    /// <summary>
    /// Gets how long the sound lasts, in seconds.
    /// </summary>
    public double DurationSeconds => _player.Duration.TotalSeconds;

    /// <summary>
    /// Gets or sets the position, in seconds; a position set from the slider is applied on the next refresh.
    /// </summary>
    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            var position = Math.Clamp(value, 0, DurationSeconds);
            if (SetProperty(ref _positionSeconds, position))
            {
                _pendingSeek = position;
                OnPropertyChanged(nameof(TimeText));
            }
        }
    }

    /// <summary>
    /// Gets the position and the length of the sound: <c>1:23 / 45:00</c>.
    /// </summary>
    public string TimeText => _localizer.Format(
        "Player.Time",
        DisplayText.Duration(TimeSpan.FromSeconds(_positionSeconds)),
        DisplayText.Duration(_player.Duration));

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets a value indicating whether the sound is off: the volume is at zero, turned there by the button or the slider.
    /// </summary>
    public bool IsMuted => Volume <= 0;

    /// <summary>
    /// Follows the sound: applies a position chosen on the slider, then shows where the player is and what it does.
    /// </summary>
    public void Refresh()
    {
        if (_pendingSeek is { } seek)
        {
            _pendingSeek = null;
            _player.Seek(TimeSpan.FromSeconds(seek));
        }

        _positionSeconds = _player.Position.TotalSeconds;
        OnPropertyChanged(nameof(PositionSeconds));
        OnPropertyChanged(nameof(TimeText));
        IsPlaying = _player.State == PlaybackState.Playing;
        Error = _player.Failure is { } failure ? _localizer.Format("Player.Failed", _localizer.Describe(failure)) : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _player.Dispose();
    }

    /// <summary>
    /// Gives the new speed to the player.
    /// </summary>
    /// <param name="value">The chosen speed.</param>
    partial void OnSelectedSpeedChanged(SpeedOptionViewModel value)
    {
        _player.Speed = value.Factor;
    }

    /// <summary>
    /// Gives the new volume to the player.
    /// </summary>
    /// <param name="value">The volume, from 0 to 100.</param>
    partial void OnVolumeChanged(double value)
    {
        _player.Volume = Math.Clamp(value, 0, 100) / 100;
    }

    /// <summary>
    /// Turns the sound off, remembering its volume, or turns it back on with the volume it had.
    /// </summary>
    [RelayCommand]
    private void ToggleMute()
    {
        if (IsMuted)
        {
            Volume = _volumeBeforeMute > 0 ? _volumeBeforeMute : FULL_VOLUME;

            return;
        }

        _volumeBeforeMute = Volume;
        Volume = 0;
    }

    /// <summary>
    /// Plays the sound, or pauses it when it plays.
    /// </summary>
    [RelayCommand]
    private void TogglePlay()
    {
        if (_player.State == PlaybackState.Playing)
        {
            _player.Pause();
        }
        else
        {
            Refresh();
            _player.Play();
        }

        Refresh();
    }

    /// <summary>
    /// Jumps ten seconds back.
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        Jump(-_jump);
    }

    /// <summary>
    /// Jumps ten seconds forward.
    /// </summary>
    [RelayCommand]
    private void Forward()
    {
        Jump(_jump);
    }

    /// <summary>
    /// Jumps from the current position.
    /// </summary>
    /// <param name="offset">How far, back when negative.</param>
    private void Jump(TimeSpan offset)
    {
        _pendingSeek = null;
        _player.Seek(_player.Position + offset);
        Refresh();
    }
}
