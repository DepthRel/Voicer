using System.Formats.Tar;
using System.IO.Compression;
using Voicer.Localization;

namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// Unpacks a checked archive of llama.cpp. The names in an archive are untrusted: an entry, or the target of a link,
/// that would land outside the destination folder stops the unpacking.
/// </summary>
/// <remarks>
/// A check of the names alone is not enough once links are unpacked: a link to <c>.</c> and then a link to <c>..</c>
/// through it would lead out. So no entry is unpacked through a link already on the disk, a file or a link takes the
/// place of a link instead of writing through it, and the target of a link goes up (<c>..</c>) only at its beginning.
/// Every link then leads to a place inside the folder, and so does every chain of them.
/// </remarks>
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
            ReplaceLink(target);
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
                    ReplaceLink(target);
                    entry.ExtractToFile(target, overwrite: true);
                    break;

                case TarEntryType.SymbolicLink:
                    // The libraries of Linux and macOS are reached through links next to them; a link may only point
                    // inside the folder.
                    CheckLinkTarget(root, target, entry.Name, entry.LinkName);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    ReplaceLink(target);
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
    /// Turns the name of an entry into its place in the destination folder, which no link already unpacked leads to.
    /// </summary>
    /// <param name="root">The full path of the destination folder, ending with a separator.</param>
    /// <param name="name">The name of the entry in the archive.</param>
    /// <returns>The full path of the entry.</returns>
    /// <exception cref="InvalidDataException">The entry would land outside the folder, or go through a link.</exception>
    private static string Target(string root, string name)
    {
        var target = Path.GetFullPath(Path.Combine(root, name));
        if (!IsInside(root, target) || Path.IsPathRooted(name))
        {
            throw Unsafe(name);
        }

        // Every folder between the destination and the entry is a real folder, never a link.
        for (var folder = Path.GetDirectoryName(target); folder is not null && IsInside(root, folder) && !IsRoot(root, folder); folder = Path.GetDirectoryName(folder))
        {
            if (IsLink(folder))
            {
                throw Unsafe(name);
            }
        }

        return target;
    }

    /// <summary>
    /// Checks the target of a link: relative, going up only at its beginning, and inside the folder.
    /// </summary>
    /// <param name="root">The full path of the destination folder, ending with a separator.</param>
    /// <param name="target">The full path of the link.</param>
    /// <param name="name">The name of the link in the archive.</param>
    /// <param name="linkName">The target of the link, as the archive gives it.</param>
    /// <exception cref="InvalidDataException">The target is absolute, goes up after a name, or leads outside.</exception>
    private static void CheckLinkTarget(string root, string target, string name, string linkName)
    {
        if (string.IsNullOrEmpty(linkName) || Path.IsPathRooted(linkName))
        {
            throw Unsafe(name);
        }

        var named = false;
        foreach (var part in linkName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                // A ".." after a name would go up from wherever a link of that name leads.
                if (named)
                {
                    throw Unsafe(name);
                }
            }
            else if (part != ".")
            {
                named = true;
            }
        }

        var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(target)!, linkName));
        if (!IsInside(root, resolved))
        {
            throw Unsafe(name);
        }
    }

    /// <summary>
    /// Removes a link at a place a file or a link is unpacked to, so the new one takes its place instead of being
    /// written through it.
    /// </summary>
    /// <param name="target">The full path of the place.</param>
    private static void ReplaceLink(string target)
    {
        if (IsLink(target))
        {
            File.Delete(target);
        }
    }

    /// <summary>
    /// Tells whether a path is a symbolic link (or a junction of Windows).
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> for a link; <see langword="false"/> for anything else or nothing.</returns>
    private static bool IsLink(string path)
    {
        return new FileInfo(path).LinkTarget is not null;
    }

    /// <summary>
    /// Tells whether a full path is the destination folder or inside it.
    /// </summary>
    /// <param name="root">The full path of the destination folder, ending with a separator.</param>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> when the path is inside.</returns>
    private static bool IsInside(string root, string path)
    {
        return path.StartsWith(root, Comparison()) || IsRoot(root, path);
    }

    /// <summary>
    /// Tells whether a full path is the destination folder itself.
    /// </summary>
    /// <param name="root">The full path of the destination folder, ending with a separator.</param>
    /// <param name="path">The full path.</param>
    /// <returns><see langword="true"/> for the folder itself.</returns>
    private static bool IsRoot(string root, string path)
    {
        return string.Equals(Path.TrimEndingDirectorySeparator(path), Path.TrimEndingDirectorySeparator(root), Comparison());
    }

    /// <summary>
    /// Gives how the file system of the platform compares names.
    /// </summary>
    /// <returns>Ignoring the case on Windows and macOS, exact elsewhere.</returns>
    private static StringComparison Comparison()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    /// <summary>
    /// Creates the failure of an entry that would lead outside the folder.
    /// </summary>
    /// <param name="name">The name of the entry.</param>
    /// <returns>The failure.</returns>
    private static InvalidDataException Unsafe(string name)
    {
        return LocalizedError.Create(m => new InvalidDataException(m), "LlamaCpp.ArchiveUnsafe", name);
    }
}
