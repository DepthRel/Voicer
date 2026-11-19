using Voicer.Core.Playback;

namespace Voicer.Media;

/// <summary>
/// Decodes a sound file into raw samples for the audio output (<see cref="PcmFormat"/>), from a moment and at a speed.
/// </summary>
public interface IPcmDecoder
{
    /// <summary>
    /// Starts decoding.
    /// </summary>
    /// <param name="file">The sound file.</param>
    /// <param name="start">The moment of the sound to start from.</param>
    /// <param name="tempo">
    /// The speed: <c>1</c> as recorded, from <see cref="IAudioPlayer.MIN_SPEED"/> to <see cref="IAudioPlayer.MAX_SPEED"/>;
    /// the pitch of the voice stays.
    /// </param>
    /// <returns>
    /// The stream of the samples; it ends with the sound. A failure of the decoder is thrown by its reading as an
    /// <see cref="IOException"/>. Disposing it stops the decoder.
    /// </returns>
    /// <exception cref="FileNotFoundException">FFmpeg is not found.</exception>
    Stream Open(string file, TimeSpan start, double tempo);
}
