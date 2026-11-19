namespace Voicer.Downloads.Enums;

/// <summary>
/// What a download is doing.
/// </summary>
public enum DownloadStage
{
    /// <summary>
    /// The file is being received.
    /// </summary>
    Downloading,

    /// <summary>
    /// The received file is being checked against its SHA-256.
    /// </summary>
    Verifying,

    /// <summary>
    /// The received file is being unpacked or put in place.
    /// </summary>
    Installing,
}
