namespace Voicer.Core.Models;

/// <summary>
/// Removes the files of a model from the disk, with its folder once nothing else is left in it.
/// </summary>
internal static class ModelFiles
{
    /// <summary>
    /// Deletes the files of a model; a file that is not there is skipped.
    /// </summary>
    /// <param name="files">The files of the model.</param>
    /// <param name="folder">The folder of the model; it goes too when it is left empty.</param>
    /// <exception cref="IOException">A file is in use or the disk failed.</exception>
    /// <exception cref="UnauthorizedAccessException">A file may not be deleted.</exception>
    internal static void Delete(IEnumerable<string> files, string folder)
    {
        foreach (var file in files)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
        }
    }
}
