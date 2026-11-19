using Voicer.Core.Enums;

namespace Voicer.Core.Playback;

/// <summary>
/// Plays the sound of a record: start and pause, a position to jump to, the speed (the pitch of the voice stays) and
/// the volume. The methods return at once; the sound is decoded and played in the background.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>
    /// The slowest speed: twice as slow as the record.
    /// </summary>
    const double MIN_SPEED = 0.5;

    /// <summary>
    /// The fastest speed: twice as fast as the record.
    /// </summary>
    const double MAX_SPEED = 2.0;

    /// <summary>
    /// Gets how long the sound lasts.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets the moment of the sound being played now.
    /// </summary>
    TimeSpan Position { get; }

    /// <summary>
    /// Gets what the player is doing.
    /// </summary>
    PlaybackState State { get; }

    /// <summary>
    /// Gets why the playback stopped by itself (the sound device or the decoder failed), or <see langword="null"/>.
    /// </summary>
    Exception? Failure { get; }

    /// <summary>
    /// Gets or sets the speed: <c>1</c> is the speed of the record, from <see cref="MIN_SPEED"/> to
    /// <see cref="MAX_SPEED"/>.
    /// </summary>
    double Speed { get; set; }

    /// <summary>
    /// Gets or sets the volume, from <c>0</c> (silent) to <c>1</c> (as recorded).
    /// </summary>
    double Volume { get; set; }

    /// <summary>
    /// Starts or resumes playing; after the end, plays from the beginning.
    /// </summary>
    void Play();

    /// <summary>
    /// Pauses at the current position.
    /// </summary>
    void Pause();

    /// <summary>
    /// Jumps to a moment of the sound; playing goes on from there.
    /// </summary>
    /// <param name="position">The moment; it is kept between the beginning and the end.</param>
    void Seek(TimeSpan position);
}
