namespace Voicer.UI.Services.Diagnostics;

/// <summary>
/// Keeps the unexpected failures of the window, so a problem can be reported with its details.
/// </summary>
public interface IErrorLog
{
    /// <summary>
    /// Gets the file the failures are written to.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Writes a failure; a log that cannot be written is skipped, the failure is still shown.
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <returns><see langword="true"/> when the failure was written.</returns>
    bool Write(Exception failure);
}
