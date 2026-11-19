using System.Net;
using System.Net.Http.Headers;

namespace Voicer.Downloads.Tests;

/// <summary>
/// An HTTP handler that serves one file from memory, honouring a range request when it is told to.
/// </summary>
internal sealed class FakeFileServer : HttpMessageHandler
{
    /// <summary>
    /// The content of the file.
    /// </summary>
    private readonly byte[] _content;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeFileServer"/> class.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    public FakeFileServer(byte[] content)
    {
        _content = content;
    }

    /// <summary>
    /// Gets or sets a value indicating whether a range request is answered with the rest of the file.
    /// </summary>
    public bool SupportsRanges { get; set; } = true;

    /// <summary>
    /// Gets or sets the status of every answer; the file is sent only with a success status.
    /// </summary>
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// Gets the start of the range the last request asked for, or <see langword="null"/>.
    /// </summary>
    public long? LastRangeStart { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRangeStart = request.Headers.Range?.Ranges.First().From;
        if (Status != HttpStatusCode.OK)
        {
            return Task.FromResult(new HttpResponseMessage(Status));
        }

        if (SupportsRanges && LastRangeStart is { } start)
        {
            var rest = new ByteArrayContent(_content[(int)start..]);
            rest.Headers.ContentRange = new ContentRangeHeaderValue(start, _content.Length - 1, _content.Length);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = rest });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_content) });
    }
}
