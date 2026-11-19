using System.Formats.Tar;
using System.IO.Compression;
using Voicer.Localization;

namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// Unpacks a checked archive of llama.cpp. The names in an archive are untrusted: an entry, or the target of a link,
/// that would land outside the destination folder stops the unpacking.
/// </summary>
internal static class ArchiveExtractor
{
    /// <summary>
    /// Unpacks a ZIP archive.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="destination">The folder to unpack into; it is created.</param>
    /// <exception cref="InvalidDataException">An entry leads outside the folder, or the archive is damaged.</exception>
    internal static void ExtractZip(string archive, string destination)
    {
        var root = Prepare(destination);

        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var target = Target(root, entry.FullName);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);

                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>
    /// Unpacks a gzipped tar archive, keeping the permissions of its files (the programs stay executable) and its
    /// links between the libraries.
    /// </summary>
    /// <param name="archive">The archive.</param>
    /// <param name="destination">The folder to unpack into; it is created.</param>
    /// <exception cref="InvalidDataException">An entry or a link leads outside the folder, or the archive is damaged.</exception>
    internal static void ExtractTarGz(string archive, string destination)
    {
        var root = Prepare(destination);

        using var file = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            var target = Target(root, entry.Name);
            switch (entry.EntryType)
            {
                case TarEntryType.Directory:
                    Directory.CreateDirectory(target);
                    break;

                case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                    break;

                case TarEntryType.SymbolicLink:
                    // The libraries of Linux and macOS are reached through links next to them; a link may only point
                    // inside the folder.
                    Target(root, Path.Combine(Path.GetDirectoryName(entry.Name) ?? string.Empty, entry.LinkName));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Delete(target);
                    File.CreateSymbolicLink(target, entry.LinkName);
                    break;

                default:
                    // Hard links, devices and the metadata of tar are not needed by the program.
                    break;
            }
        }
    }

    /// <summary>
    /// Creates the destination folder.
    /// </summary>
    /// <param name="destination">The folder.</param>
    /// <returns>Its full path, ending with a separator.</returns>
    private static string Prepare(string destination)
    {
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);

        return Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Turns the name of an entry into its place in the destination folder.
    /// </summary>
    /// <param name="root">The full path of the destination folder, ending with a separator.</param>
    /// <param name="name">The name of the entry in the archive.</param>
    /// <returns>The full path of the entry.</returns>
    /// <exception cref="InvalidDataException">The entry would land outside the folder.</exception>
    private static string Target(string root, string name)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var target = Path.GetFullPath(Path.Combine(root, name));
        var inside = target.StartsWith(root, comparison) ||
            string.Equals(target + Path.DirectorySeparatorChar, root, comparison);
        if (!inside || Path.IsPathRooted(name))
        {
            throw LocalizedError.Create(m => new InvalidDataException(m), "LlamaCpp.ArchiveUnsafe", name);
        }

        return target;
    }
}
