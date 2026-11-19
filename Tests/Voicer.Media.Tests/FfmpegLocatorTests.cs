using FluentAssertions;
using Voicer.Media.Ffmpeg;

namespace Voicer.Media.Tests;

/// <summary>
/// Tests of the search for FFmpeg.
/// </summary>
public sealed class FfmpegLocatorTests
{
    /// <summary>
    /// The tools folder of the application.
    /// </summary>
    private static readonly string _tools = Path.Combine(Path.GetTempPath(), "voicer-tools");

    /// <summary>
    /// The copy the application installed comes before the program of the system.
    /// </summary>
    [Fact]
    public void Locate_PrefersTheInstalledCopy()
    {
        var installed = Path.Combine(_tools, "FFmpeg", "ffmpeg.exe");
        var system = Path.Combine(Path.GetTempPath(), "bin", "ffmpeg.exe");
        var locator = new FfmpegLocator(_tools, isWindows: true, environment: _ => Path.Combine(Path.GetTempPath(), "bin"), fileExists: f => f == installed || f == system);

        locator.Locate().Should().Be(installed);
    }

    /// <summary>
    /// The folders of PATH are searched; a relative one is not trusted.
    /// </summary>
    [Fact]
    public void Locate_SearchesTheFullFoldersOfPath()
    {
        var full = Path.Combine(Path.GetTempPath(), "tools-bin");
        var path = string.Join(';', "relative", full);
        var candidates = new List<string>();
        var locator = new FfmpegLocator(_tools, isWindows: true, environment: _ => path, fileExists: f =>
        {
            candidates.Add(f);

            return f == Path.Combine(full, "ffmpeg.exe");
        });

        locator.Locate().Should().Be(Path.Combine(full, "ffmpeg.exe"));
        candidates.Should().NotContain(c => c.StartsWith("relative", StringComparison.Ordinal));
    }

    /// <summary>
    /// On Linux and macOS the places of the package managers are searched, so an application started from the desktop
    /// finds a program of Homebrew.
    /// </summary>
    [Fact]
    public void Locate_SearchesThePackageManagersOnUnix()
    {
        var homebrew = Path.Combine("/opt/homebrew/bin", "ffmpeg");
        var locator = new FfmpegLocator(_tools, isWindows: false, environment: _ => null, fileExists: f => f == homebrew);

        locator.Locate().Should().Be(homebrew);
        locator.ExecutableName.Should().Be("ffmpeg");
    }

    /// <summary>
    /// Nothing found is <see langword="null"/>.
    /// </summary>
    [Fact]
    public void Locate_ReturnsNullWhenMissing()
    {
        new FfmpegLocator(_tools, isWindows: true, environment: _ => null, fileExists: _ => false).Locate().Should().BeNull();
    }
}
