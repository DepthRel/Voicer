using FluentAssertions;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the finding of the programs of the pinned build of llama.cpp.
/// </summary>
public sealed class LlamaCppLocatorTests : IDisposable
{
    /// <summary>
    /// A temporary folder of the tools.
    /// </summary>
    private readonly string _tools = Path.Combine(Path.GetTempPath(), "voicer-llama-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tools))
        {
            Directory.Delete(_tools, recursive: true);
        }
    }

    /// <summary>
    /// Both programs are found in the folder the archive of the pinned build unpacks to, each by its own name.
    /// </summary>
    [Fact]
    public void Locate_FindsEachProgramOfThePinnedBuild()
    {
        var locator = new LlamaCppLocator(_tools, isWindows: true);
        var folder = Path.Combine(locator.InstallDirectory, "llama-" + LlamaCppPackage.VERSION);
        Directory.CreateDirectory(folder);

        locator.LocateServer().Should().BeNull();

        File.WriteAllBytes(Path.Combine(folder, "llama-server.exe"), []);

        locator.LocateServer().Should().Be(Path.Combine(folder, "llama-server.exe"));
        locator.Locate().Should().BeNull();
    }

    /// <summary>
    /// Outside Windows the programs have no extension.
    /// </summary>
    [Fact]
    public void Locate_TakesTheNamesOfTheSystem()
    {
        var locator = new LlamaCppLocator(_tools, isWindows: false);

        locator.ExecutableName.Should().Be("llama-tts");
        locator.ServerName.Should().Be("llama-server");
    }
}
