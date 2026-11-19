using Voicer.Downloads.Enums;

namespace Voicer.Downloads;

/// <summary>
/// How far a download got.
/// </summary>
/// <param name="Stage">What the download is doing.</param>
/// <param name="DoneBytes">How many bytes of the file are received (or checked, while verifying).</param>
/// <param name="TotalBytes">The size of the file, in bytes.</param>
/// <param name="BytesPerSecond">The current speed of the stage, in bytes per second; <c>0</c> when not yet known.</param>
public sealed record DownloadProgress(DownloadStage Stage, long DoneBytes, long TotalBytes, double BytesPerSecond)
{
    /// <summary>
    /// Gets how much of the stage is done, from 0 to 100.
    /// </summary>
    public double Percent => TotalBytes <= 0 ? 0 : Math.Clamp(DoneBytes * 100.0 / TotalBytes, 0, 100);
}
