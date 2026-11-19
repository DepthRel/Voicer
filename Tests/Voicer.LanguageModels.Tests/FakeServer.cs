using System.Net;
using System.Text;

namespace Voicer.LanguageModels.Tests;

/// <summary>
/// A server of language models for the tests: it answers every request with the same prepared answer and keeps what it
/// was asked.
/// </summary>
internal sealed class FakeServer : HttpMessageHandler
{
    /// <summary>
    /// The status of the answer.
    /// </summary>
    private readonly HttpStatusCode _status;

    /// <summary>
    /// The body of the answer.
    /// </summary>
    private readonly string _body;

    /// <summary>
    /// The type of the body of the answer.
    /// </summary>
    private readonly string _mediaType;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeServer"/> class.
    /// </summary>
    /// <param name="status">The status of the answer.</param>
    /// <param name="body">The body of the answer.</param>
    /// <param name="mediaType">The type of the body.</param>
    internal FakeServer(HttpStatusCode status, string body, string mediaType)
    {
        _status = status;
        _body = body;
        _mediaType = mediaType;
    }

    /// <summary>
    /// Gets the last request.
    /// </summary>
    internal HttpRequestMessage? Request { get; private set; }

    /// <summary>
    /// Gets the body of the last request.
    /// </summary>
    internal string? RequestBody { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, _mediaType),
        };
    }
}
