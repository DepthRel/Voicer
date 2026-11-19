namespace Voicer.Core.Enums;

/// <summary>
/// What a player of the sound of a record is doing.
/// </summary>
public enum PlaybackState
{
    /// <summary>
    /// Not playing: before the first start, or paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Playing.
    /// </summary>
    Playing,

    /// <summary>
    /// Played to the end; the next start begins from the beginning.
    /// </summary>
    Ended,
}
