namespace Voicer.Core.Models;

/// <summary>
/// The detector of speech that runs before a recognition: it finds where people speak, so silence, music and noise
/// never reach the speech model, which would make up text for them. The file is pinned like every download.
/// </summary>
/// <param name="Name">The name of the detector.</param>
/// <param name="Folder">The folder of the detector in the models folder.</param>
/// <param name="FileName">The name of its file.</param>
/// <param name="DownloadUri">Where the file is downloaded from, over HTTPS.</param>
/// <param name="SizeBytes">The exact size of the file.</param>
/// <param name="Sha256">The SHA-256 of the file, in lower-case hexadecimal.</param>
public sealed record SpeechDetectorModel(string Name, string Folder, string FileName, Uri DownloadUri, long SizeBytes, string Sha256)
{
    /// <summary>
    /// Gets Silero VAD 6.2.0 in the ggml format of whisper.cpp (MIT), from a fixed revision of
    /// <c>ggml-org/whisper-vad</c>.
    /// </summary>
    public static SpeechDetectorModel SileroVad { get; } = new(
        "Silero VAD 6.2.0",
        "silero-vad",
        "ggml-silero-v6.2.0.bin",
        new Uri("https://huggingface.co/ggml-org/whisper-vad/resolve/9ffd54a1e1ee413ddf265af9913beaf518d1639b/ggml-silero-v6.2.0.bin"),
        885_098,
        "2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987");
}
