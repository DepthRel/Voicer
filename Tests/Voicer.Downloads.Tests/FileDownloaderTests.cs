using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Voicer.Localization;

namespace Voicer.Downloads.Tests;

/// <summary>
/// Tests of the downloads that are checked before they are used.
/// </summary>
public sealed class FileDownloaderTests : IDisposable
{
    /// <summary>
    /// The content of the served file.
    /// </summary>
    private static readonly byte[] _content = [.. Enumerable.Range(0, 300_000).Select(i => (byte)(i * 7))];

    /// <summary>
    /// The address of the served file.
    /// </summary>
    private static readonly Uri _source = new("https://example.org/model.bin");

    /// <summary>
    /// A temporary folder of the downloads.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-downloads-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The served file.
    /// </summary>
    private readonly FakeFileServer _server = new(_content);

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// The whole file is received, checked and put in place; nothing is left behind.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_PutsTheCheckedFileInPlace()
    {
        var request = Request(Sha(_content));

        await Downloader().DownloadAsync(request, null, CancellationToken.None);

        File.ReadAllBytes(request.Destination).Should().Equal(_content);
        File.Exists(request.Destination + FileDownloader.PART_EXTENSION).Should().BeFalse();
        _server.LastRangeStart.Should().BeNull();
    }

    /// <summary>
    /// An interrupted download continues from the received part.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_ContinuesFromThePart()
    {
        var request = Request(Sha(_content));
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(request.Destination + FileDownloader.PART_EXTENSION, _content[..100_000]);

        await Downloader().DownloadAsync(request, null, CancellationToken.None);

        _server.LastRangeStart.Should().Be(100_000);
        File.ReadAllBytes(request.Destination).Should().Equal(_content);
    }

    /// <summary>
    /// A server that ignores the range sends the whole file, and the part starts over.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_StartsOverWhenTheServerIgnoresTheRange()
    {
        _server.SupportsRanges = false;
        var request = Request(Sha(_content));
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(request.Destination + FileDownloader.PART_EXTENSION, [9, 9, 9]);

        await Downloader().DownloadAsync(request, null, CancellationToken.None);

        File.ReadAllBytes(request.Destination).Should().Equal(_content);
    }

    /// <summary>
    /// A file whose SHA-256 differs is refused and removed.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_RefusesAWrongFile()
    {
        var request = Request(new string('0', 64));

        var download = () => Downloader().DownloadAsync(request, null, CancellationToken.None);

        await download.Should().ThrowAsync<InvalidDataException>();
        File.Exists(request.Destination).Should().BeFalse();
        File.Exists(request.Destination + FileDownloader.PART_EXTENSION).Should().BeFalse();
    }

    /// <summary>
    /// A refusal of the server is an HTTP failure with a message in the language of the reader.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_ReportsTheStatusOfTheServer()
    {
        _server.Status = HttpStatusCode.NotFound;

        var download = () => Downloader().DownloadAsync(Request(Sha(_content)), null, CancellationToken.None);

        var failure = await download.Should().ThrowAsync<HttpRequestException>();
        TestTexts.Russian.Describe(failure.Which).Should().Be(TestTexts.Russian.Format("Download.HttpFailed", 404));
    }

    /// <summary>
    /// Only HTTPS addresses are downloaded.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Download_RefusesPlainHttp()
    {
        var request = Request(Sha(_content)) with { Source = new Uri("http://example.org/model.bin") };

        var download = () => Downloader().DownloadAsync(request, null, CancellationToken.None);

        await download.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Creates the downloader over the fake server.
    /// </summary>
    /// <returns>The downloader.</returns>
    private FileDownloader Downloader()
    {
        return new FileDownloader(new HttpClient(_server, disposeHandler: false));
    }

    /// <summary>
    /// Describes the download of the served file.
    /// </summary>
    /// <param name="sha256">The expected SHA-256.</param>
    /// <returns>The request.</returns>
    private DownloadRequest Request(string sha256)
    {
        return new DownloadRequest(_source, Path.Combine(_folder, "model.bin"), _content.Length, sha256);
    }

    /// <summary>
    /// Computes the SHA-256 of bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash in lower-case hexadecimal.</returns>
    private static string Sha(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
