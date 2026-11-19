namespace Voicer.UI.Services.Transcription;

/// <summary>
/// Checks a file chosen for the recognition before the page takes it, away from the window, so a slow or broken disk
/// never freezes it.
/// </summary>
public interface ISourceFileInspector
{
    /// <summary>
    /// Checks a file.
    /// </summary>
    /// <param name="path">The file, as the user chose it.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The checked file.</returns>
    /// <exception cref="IOException">The file is missing, empty or cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be read.</exception>
    Task<SourceFile> InspectAsync(string path, CancellationToken cancellationToken);
}
