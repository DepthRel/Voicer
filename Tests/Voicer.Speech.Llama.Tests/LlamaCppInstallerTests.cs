using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using FluentAssertions;
using NSubstitute;
using Voicer.Downloads;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the pinned builds of llama.cpp, their installation and the unpacking of untrusted archives.
/// </summary>
public sealed class LlamaCppInstallerTests : IDisposable
{
    /// <summary>
    /// A temporary tools folder.
    /// </summary>
    private readonly string _tools = Path.Combine(Path.GetTempPath(), "voicer-llama-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The downloader of the archive.
    /// </summary>
    private readonly IFileDownloader _downloader = Substitute.For<IFileDownloader>();

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
    /// Windows and Linux on x64 and macOS on Apple silicon have a build, pinned by size and SHA-256 and taken over HTTPS
    /// from the pinned release; other systems have none.
    /// </summary>
    [Fact]
    public void Package_HasAPinnedBuildPerSupportedSystem()
    {
        var builds = new[]
        {
            LlamaCppPackage.For(OSPlatform.Windows, Architecture.X64),
            LlamaCppPackage.For(OSPlatform.Linux, Architecture.X64),
            LlamaCppPackage.For(OSPlatform.OSX, Architecture.Arm64),
        };

        builds.Should().OnlyContain(b => b != null);
        foreach (var build in builds)
        {
            build!.Source.Scheme.Should().Be(Uri.UriSchemeHttps);
            build.Source.AbsolutePath.Should().Contain("/" + LlamaCppPackage.VERSION + "/");
            build.SizeBytes.Should().BePositive();
            build.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        LlamaCppPackage.WindowsX64.IsZip.Should().BeTrue();
        LlamaCppPackage.LinuxX64.IsZip.Should().BeFalse();
        LlamaCppPackage.For(OSPlatform.OSX, Architecture.X64).Should().BeNull();
        LlamaCppPackage.For(OSPlatform.Windows, Architecture.Arm64).Should().BeNull();
    }

    /// <summary>
    /// The checked archive is unpacked into the folder of the pinned build, the archive and older builds are removed,
    /// and llama-tts is found there.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_UnpacksTheBuild_AndRemovesTheOldOnes()
    {
        var locator = new LlamaCppLocator(_tools, isWindows: true);
        var old = Path.Combine(locator.RootDirectory, "b1");
        Directory.CreateDirectory(old);
        ServeZip(("llama-tts.exe", "program"), ("ggml-vulkan.dll", "library"));

        var path = await new LlamaCppInstaller(_downloader, locator, LlamaCppPackage.WindowsX64).InstallAsync(null, CancellationToken.None);

        path.Should().Be(Path.Combine(locator.InstallDirectory, "llama-tts.exe"));
        locator.Locate().Should().Be(path);
        File.Exists(Path.Combine(locator.InstallDirectory, "ggml-vulkan.dll")).Should().BeTrue();
        Directory.Exists(old).Should().BeFalse();
        Directory.EnumerateFiles(locator.RootDirectory).Should().BeEmpty("the archive is removed");
    }

    /// <summary>
    /// An entry of a ZIP archive that leads out of the folder stops the unpacking, and nothing is installed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_RefusesAnEntryOutsideTheFolder()
    {
        var locator = new LlamaCppLocator(_tools, isWindows: true);
        ServeZip(("llama-tts.exe", "program"), ("../escaped.txt", "outside"));

        var act = () => new LlamaCppInstaller(_downloader, locator, LlamaCppPackage.WindowsX64).InstallAsync(null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
        File.Exists(Path.Combine(locator.RootDirectory, "escaped.txt")).Should().BeFalse();
        locator.Locate().Should().BeNull();
    }

    /// <summary>
    /// A system without a build cannot install one.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_WithoutABuild_IsRefused()
    {
        var installer = new LlamaCppInstaller(_downloader, new LlamaCppLocator(_tools), null);

        installer.CanInstall.Should().BeFalse();
        await installer.Invoking(i => i.InstallAsync(null, CancellationToken.None)).Should().ThrowAsync<PlatformNotSupportedException>();
    }

    /// <summary>
    /// A gzipped tar archive, as Linux and macOS get, is unpacked with its folder; an entry that leads out is refused.
    /// </summary>
    [Fact]
    public void TarGz_IsUnpacked_AndAnEscapeIsRefused()
    {
        Directory.CreateDirectory(_tools);
        var good = TarGz("good.tar.gz", ("llama-b1/llama-tts", "program"));
        var bad = TarGz("bad.tar.gz", ("../../escaped", "outside"));

        ArchiveExtractor.ExtractTarGz(good, Path.Combine(_tools, "good"));
        var act = () => ArchiveExtractor.ExtractTarGz(bad, Path.Combine(_tools, "bad"));

        File.ReadAllText(Path.Combine(_tools, "good", "llama-b1", "llama-tts")).Should().Be("program");
        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A link that goes up after a name is refused before anything is linked: where it leads would depend on the link
    /// of that name.
    /// </summary>
    [Fact]
    public void TarGz_LinkGoingUpAfterAName_IsRefused()
    {
        Directory.CreateDirectory(_tools);
        var bad = TarGz("bad.tar.gz", ("llama-b1/lib.so", "link:y/../lib.so.1"));

        var act = () => ArchiveExtractor.ExtractTarGz(bad, Path.Combine(_tools, "bad"));

        act.Should().Throw<InvalidDataException>();
    }

    /// <summary>
    /// A chain of links whose names each stay inside (a link to <c>.</c>, then a link to <c>..</c> through it) does not
    /// lead a file out: nothing is unpacked through a link. Needs the right to create links (on Windows, the developer
    /// mode); without it there is nothing to check.
    /// </summary>
    [Fact]
    public void TarGz_ChainOfLinks_DoesNotLeadOut()
    {
        Directory.CreateDirectory(_tools);
        var probe = Path.Combine(_tools, "probe");
        try
        {
            File.CreateSymbolicLink(probe, "target");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var bad = TarGz("bad.tar.gz", ("x", "link:."), ("x/l", "link:.."), ("l/evil", "outside"));
        var destination = Path.Combine(_tools, "inside", "bad");

        var act = () => ArchiveExtractor.ExtractTarGz(bad, destination);

        act.Should().Throw<InvalidDataException>();
        File.Exists(Path.Combine(_tools, "inside", "evil")).Should().BeFalse();
    }

    /// <summary>
    /// Makes the downloader write a ZIP archive with the given files where it is asked to.
    /// </summary>
    /// <param name="entries">The names and contents of the files.</param>
    private void ServeZip(params (string Name, string Content)[] entries)
    {
        _downloader.DownloadAsync(default!, default, default).ReturnsForAnyArgs(call =>
        {
            var request = call.Arg<DownloadRequest>();
            using var zip = ZipFile.Open(request.Destination, ZipArchiveMode.Create);
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Writes a gzipped tar archive with the given files.
    /// </summary>
    /// <param name="name">The name of the archive.</param>
    /// <param name="entries">The names and contents of the files; a content <c>link:target</c> makes a symbolic link.</param>
    /// <returns>The full path of the archive.</returns>
    private string TarGz(string name, params (string Name, string Content)[] entries)
    {
        const string LINK = "link:";
        var path = Path.Combine(_tools, name);
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        using var writer = new TarWriter(gzip);
        foreach (var (entryName, content) in entries)
        {
            if (content.StartsWith(LINK, StringComparison.Ordinal))
            {
                writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, entryName) { LinkName = content[LINK.Length..] });

                continue;
            }

            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
            {
                DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),
            };
            writer.WriteEntry(entry);
        }

        return path;
    }
}
