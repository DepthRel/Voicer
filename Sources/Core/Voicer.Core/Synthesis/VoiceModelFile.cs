namespace Voicer.Core.Synthesis;

/// <summary>
/// One file of a voice model, pinned by its size and SHA-256, so a download that was damaged or replaced on the way is
/// refused.
/// </summary>
/// <param name="FileName">The name of the file in the folder of the model.</param>
/// <param name="DownloadUri">Where the file is downloaded from.</param>
/// <param name="SizeBytes">The exact size of the file, in bytes.</param>
/// <param name="Sha256">The SHA-256 of the file, in lower-case hexadecimal.</param>
public sealed record VoiceModelFile(string FileName, Uri DownloadUri, long SizeBytes, string Sha256);
