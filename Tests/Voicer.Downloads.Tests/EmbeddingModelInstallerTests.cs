using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.Downloads.Models;

namespace Voicer.Downloads.Tests;

/// <summary>
/// Tests of the download of a model of the search by meaning.
/// </summary>
public sealed class EmbeddingModelInstallerTests : IDisposable
{
    /// <summary>
    /// A model of 100 bytes.
    /// </summary>
    private static readonly EmbeddingModel _model = new(
        "tiny-embed",
        "Tiny Embed",
        "tiny.gguf",
        new Uri("https://example.org/tiny.gguf"),
        100,
        new string('a', 64),
        512);

    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-embed-install-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The downloader of the file.
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
    /// The file is downloaded with its pin into the folder of the model.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_DownloadsThePinnedFile()
    {
        var store = new EmbeddingModelStore(_root, [_model]);

        await new EmbeddingModelInstaller(_downloader, store).InstallAsync(_model, null, CancellationToken.None);

        await _downloader.Received(1).DownloadAsync(
            new DownloadRequest(_model.DownloadUri, Path.Combine(_root, "tiny-embed", "tiny.gguf"), 100, _model.Sha256),
            null,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A model already there is not downloaded again.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Install_KeepsAnInstalledModel()
    {
        var store = new EmbeddingModelStore(_root, [_model]);
        Directory.CreateDirectory(store.DirectoryOf(_model));
        await File.WriteAllBytesAsync(store.FileOf(_model), new byte[100]);

        await new EmbeddingModelInstaller(_downloader, store).InstallAsync(_model, null, CancellationToken.None);

        await _downloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default, default);
    }
}
