using FluentAssertions;
using NSubstitute;
using Voicer.Core.Synthesis;
using Voicer.Downloads.Enums;
using Voicer.Downloads.Models;

namespace Voicer.Downloads.Tests;

/// <summary>
/// Tests of the download of the files of a voice model as one download.
/// </summary>
public sealed class VoiceModelInstallerTests : IDisposable
{
    /// <summary>
    /// A model of two files: 100 bytes of weights and 50 bytes of projector.
    /// </summary>
    private static readonly VoiceModel _model = new(
        "tiny-voice",
        "Tiny Voice",
        new VoiceModelFile("weights.gguf", new Uri("https://example.org/weights.gguf"), 100, new string('a', 64)),
        new VoiceModelFile("mmproj.gguf", new Uri("https://example.org/mmproj.gguf"), 50, new string('b', 64)),
        ["en"],
        [],
        CanCloneVoice: false,
        NeedsSample: false,
        RunsOnCpu: false);

    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-voice-install-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The downloader of the files.
    /// </summary>
    private readonly IFileDownloader _downloader = Substitute.For<IFileDownloader>();

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// Every file is downloaded with its pin into the folder of the model, and the progress is that of the whole model.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_DownloadsEveryFile_AndReportsTheWholeModel()
    {
        var store = new VoiceModelStore(_root, [_model]);
        var requests = new List<DownloadRequest>();
        _downloader.DownloadAsync(default!, default, default).ReturnsForAnyArgs(call =>
        {
            var request = call.Arg<DownloadRequest>();
            requests.Add(request);
            call.Arg<IProgress<DownloadProgress>?>()!.Report(new DownloadProgress(DownloadStage.Downloading, 10, request.SizeBytes, 0));

            return Task.CompletedTask;
        });
        var reports = new List<DownloadProgress>();

        await new VoiceModelInstaller(_downloader, store).InstallAsync(_model, new SyncProgress(reports.Add), CancellationToken.None);

        requests.Should().Equal(
            new DownloadRequest(_model.Weights.DownloadUri, store.FileOf(_model, _model.Weights), 100, _model.Weights.Sha256),
            new DownloadRequest(_model.Projector.DownloadUri, store.FileOf(_model, _model.Projector), 50, _model.Projector.Sha256));
        reports.Select(r => (r.DoneBytes, r.TotalBytes)).Should().Equal((10L, 150L), (110L, 150L));
    }

    /// <summary>
    /// A file that is already there whole is kept, not downloaded again.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_KeepsAFileThatIsAlreadyThere()
    {
        var store = new VoiceModelStore(_root, [_model]);
        Directory.CreateDirectory(store.DirectoryOf(_model));
        File.WriteAllBytes(store.FileOf(_model, _model.Weights), new byte[100]);

        await new VoiceModelInstaller(_downloader, store).InstallAsync(_model, null, CancellationToken.None);

        await _downloader.Received(1).DownloadAsync(
            Arg.Is<DownloadRequest>(r => r.Source == _model.Projector.DownloadUri),
            Arg.Any<IProgress<DownloadProgress>?>(),
            Arg.Any<CancellationToken>());
        await _downloader.DidNotReceive().DownloadAsync(
            Arg.Is<DownloadRequest>(r => r.Source == _model.Weights.DownloadUri),
            Arg.Any<IProgress<DownloadProgress>?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A progress that reports on the calling thread, so the test sees every report at once.
    /// </summary>
    /// <param name="report">Receives the reports.</param>
    private sealed class SyncProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        /// <inheritdoc />
        public void Report(DownloadProgress value)
        {
            report(value);
        }
    }
}
