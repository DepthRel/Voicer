using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using Voicer.Speech.Llama.Servers;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the notes of the running servers: a server left behind by an application that ended is ended, a server of
/// a running application and a process that only took a number of a server are left alone.
/// </summary>
public sealed class ServerProcessRegistryTests : IDisposable
{
    /// <summary>
    /// A temporary folder of the notes.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-servers-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// The note of a server whose application still runs stays, and the server is not touched.
    /// </summary>
    [Fact]
    public void ServerOfARunningApplication_IsLeftAlone()
    {
        using var self = Process.GetCurrentProcess();
        var registry = new ServerProcessRegistry(_folder);

        registry.Add(self);
        registry.EndOrphans();

        File.Exists(NoteOf(self.Id)).Should().BeTrue();
        registry.Remove(self.Id);
        File.Exists(NoteOf(self.Id)).Should().BeFalse();
    }

    /// <summary>
    /// A note whose number now names another process (it started at another moment) is forgotten, the process is not
    /// ended; a damaged note is forgotten too.
    /// </summary>
    [Fact]
    public void NumberTakenByAnotherProcess_IsNotEnded()
    {
        using var self = Process.GetCurrentProcess();
        Directory.CreateDirectory(_folder);
        File.WriteAllText(NoteOf(self.Id), string.Join('\n', "0", self.Id.ToString(CultureInfo.InvariantCulture), "0"));
        File.WriteAllText(Path.Combine(_folder, "12.server"), "damaged");

        new ServerProcessRegistry(_folder).EndOrphans();

        self.HasExited.Should().BeFalse();
        Directory.EnumerateFiles(_folder).Should().BeEmpty();
    }

    /// <summary>
    /// A server whose application ended is ended, and its note forgotten.
    /// </summary>
    [Fact]
    public void ServerOfAnEndedApplication_IsEnded()
    {
        using var server = StartLongProcess();
        Directory.CreateDirectory(_folder);
        var start = server.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        File.WriteAllText(NoteOf(server.Id), string.Join('\n', start, server.Id.ToString(CultureInfo.InvariantCulture), "0"));

        new ServerProcessRegistry(_folder).EndOrphans();

        server.WaitForExit(10_000).Should().BeTrue();
        File.Exists(NoteOf(server.Id)).Should().BeFalse();
    }

    /// <summary>
    /// Starts a process that runs for a while on its own.
    /// </summary>
    /// <returns>The process.</returns>
    private static Process StartLongProcess()
    {
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping") { ArgumentList = { "-n", "60", "127.0.0.1" } }
            : new ProcessStartInfo("sleep") { ArgumentList = { "60" } };
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;

        return Process.Start(start)!;
    }

    /// <summary>
    /// Gives the file of the note of a server.
    /// </summary>
    /// <param name="id">The number of the process of the server.</param>
    /// <returns>The full path of the file.</returns>
    private string NoteOf(int id)
    {
        return Path.Combine(_folder, id.ToString(CultureInfo.InvariantCulture) + ".server");
    }
}
