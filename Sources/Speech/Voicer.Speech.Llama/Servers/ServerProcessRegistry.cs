using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// Keeps a note of every llama-server the application runs, in a folder of the data folder, so a server left behind by
/// an application that ended without stopping it (a failure, a kill) is ended on the next start. On Windows the job of
/// <see cref="ChildProcessJob"/> ends such a server at once; on Linux and macOS this is the only way.
/// </summary>
/// <remarks>
/// A note names the server and the application that started it, each by its number and the moment it started, so a
/// number the system gave to another process since is never taken for them. A server whose application still runs (a
/// second window over the same data folder) is left alone.
/// </remarks>
public sealed class ServerProcessRegistry
{
    /// <summary>
    /// The extension of a note.
    /// </summary>
    private const string EXTENSION = ".server";

    /// <summary>
    /// How far two readings of the start of one process may differ: the system keeps it rounded on some platforms.
    /// </summary>
    private static readonly TimeSpan _startTolerance = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The folder of the notes.
    /// </summary>
    private readonly string _folder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerProcessRegistry"/> class.
    /// </summary>
    /// <param name="folder">The folder of the notes; it is created with the first one.</param>
    public ServerProcessRegistry(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = Path.GetFullPath(folder);
    }

    /// <summary>
    /// Notes a started server, with the process of the application that owns it. A note that cannot be written only
    /// loses the cleanup after a failure.
    /// </summary>
    /// <param name="server">The started server.</param>
    public void Add(Process server)
    {
        ArgumentNullException.ThrowIfNull(server);

        try
        {
            using var owner = Process.GetCurrentProcess();
            var note = string.Join(
                '\n',
                Start(server).ToString(CultureInfo.InvariantCulture),
                owner.Id.ToString(CultureInfo.InvariantCulture),
                Start(owner).ToString(CultureInfo.InvariantCulture));

            Directory.CreateDirectory(_folder);
            File.WriteAllText(FileOf(server.Id), note);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The server runs without a note.
        }
    }

    /// <summary>
    /// Forgets a server the application stopped.
    /// </summary>
    /// <param name="serverId">The number of the process of the server.</param>
    public void Remove(int serverId)
    {
        Delete(FileOf(serverId));
    }

    /// <summary>
    /// Ends the servers noted by applications that no longer run, and forgets them; the notes of a running application
    /// stay.
    /// </summary>
    public void EndOrphans()
    {
        if (!Directory.Exists(_folder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_folder, "*" + EXTENSION))
        {
            if (Read(file) is not { } note)
            {
                Delete(file);

                continue;
            }

            using (var owner = Find(note.OwnerId, note.OwnerStart))
            {
                if (owner is not null)
                {
                    continue;
                }
            }

            using (var server = Find(note.ServerId, note.ServerStart))
            {
                End(server);
            }

            Delete(file);
        }
    }

    /// <summary>
    /// Reads the moment a process started, in ticks of UTC.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <returns>The moment.</returns>
    private static long Start(Process process)
    {
        return process.StartTime.ToUniversalTime().Ticks;
    }

    /// <summary>
    /// Finds a running process by its number and the moment it started.
    /// </summary>
    /// <param name="id">The number of the process.</param>
    /// <param name="start">The moment it started, in ticks of UTC.</param>
    /// <returns>The process; <see langword="null"/> when it ended, or the number now names another process.</returns>
    private static Process? Find(int id, long start)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(id);
            if (!process.HasExited && Math.Abs(Start(process) - start) <= _startTolerance.Ticks)
            {
                return process;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Gone, or a process of another user: not one of ours.
        }

        process?.Dispose();

        return null;
    }

    /// <summary>
    /// Ends a server left behind.
    /// </summary>
    /// <param name="server">The server, or <see langword="null"/> when it already ended.</param>
    private static void End(Process? server)
    {
        if (server is null)
        {
            return;
        }

        try
        {
            server.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended meanwhile, or it cannot be ended: nothing more to do.
        }
    }

    /// <summary>
    /// Reads a note.
    /// </summary>
    /// <param name="file">The file of the note.</param>
    /// <returns>The note; <see langword="null"/> when it cannot be read or is damaged.</returns>
    private static Note? Read(string file)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var hasServer = int.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None, CultureInfo.InvariantCulture, out var serverId);
        if (!hasServer ||
            lines.Length != 3 ||
            !long.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out var serverStart) ||
            !int.TryParse(lines[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerId) ||
            !long.TryParse(lines[2], NumberStyles.None, CultureInfo.InvariantCulture, out var ownerStart))
        {
            return null;
        }

        return new Note(serverId, serverStart, ownerId, ownerStart);
    }

    /// <summary>
    /// Deletes a file, if it can.
    /// </summary>
    /// <param name="file">The file.</param>
    private static void Delete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tried again on the next start.
        }
    }

    /// <summary>
    /// Gives the file of the note of a server.
    /// </summary>
    /// <param name="serverId">The number of the process of the server.</param>
    /// <returns>The full path of the file.</returns>
    private string FileOf(int serverId)
    {
        return Path.Combine(_folder, serverId.ToString(CultureInfo.InvariantCulture) + EXTENSION);
    }

    /// <summary>
    /// A note of a server: the server and the application that started it, each by its number and its start.
    /// </summary>
    /// <param name="ServerId">The number of the process of the server.</param>
    /// <param name="ServerStart">The moment the server started, in ticks of UTC.</param>
    /// <param name="OwnerId">The number of the process of the application.</param>
    /// <param name="OwnerStart">The moment the application started, in ticks of UTC.</param>
    private sealed record Note(int ServerId, long ServerStart, int OwnerId, long OwnerStart);
}
