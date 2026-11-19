using FluentAssertions;
using NSubstitute;
using Voicer.Core.Models;
using Voicer.Downloads.Models;

namespace Voicer.Downloads.Tests;

/// <summary>
/// Tests of the download of a speech model with the detector of speech.
/// </summary>
public sealed class ModelInstallerTests : IDisposable
{
    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-install-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The downloader of the files: it writes a file of the pinned size, as a real download does.
    /// </summary>
    private readonly IFileDownloader _downloader = Substitute.For<IFileDownloader>();

    /// <summary>
    /// The requests the downloader received.
    /// </summary>
    private readonly List<DownloadRequest> _requests = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelInstallerTests"/> class.
    /// </summary>
    public ModelInstallerTests()
    {
        _downloader.DownloadAsync(default!, default, default).ReturnsForAnyArgs(call =>
        {
            var request = call.Arg<DownloadRequest>();
            _requests.Add(request);
            Directory.CreateDirectory(Path.GetDirectoryName(request.Destination)!);
            using (var file = File.Create(request.Destination))
            {
                file.SetLength(request.SizeBytes);
            }

            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// The first model brings the detector with its pin into its own folder; the next model does not download it again.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_BringsTheDetectorOnce()
    {
        var small = SpeechModelCatalog.All.Last();
        var store = new ModelStore(_root);
        var installer = new ModelInstaller(_downloader, store);
        var detector = SpeechDetectorModel.SileroVad;

        await installer.InstallAsync(small, null, CancellationToken.None);
        await installer.InstallAsync(small, null, CancellationToken.None);

        _requests.Should().HaveCount(3);
        _requests[1].Should().Be(new DownloadRequest(detector.DownloadUri, Path.Combine(_root, "silero-vad", "ggml-silero-v6.2.0.bin"), detector.SizeBytes, detector.Sha256));
        store.IsDetectorInstalled(detector).Should().BeTrue();
    }

    /// <summary>
    /// The detector is pinned: HTTPS from a fixed revision, an exact size and a SHA-256.
    /// </summary>
    [Fact]
    public void Detector_IsPinned()
    {
        var detector = SpeechDetectorModel.SileroVad;

        detector.DownloadUri.Scheme.Should().Be("https");
        detector.DownloadUri.AbsolutePath.Should().Contain("/resolve/9ffd54a1e1ee413ddf265af9913beaf518d1639b/");
        detector.SizeBytes.Should().BePositive();
        detector.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
