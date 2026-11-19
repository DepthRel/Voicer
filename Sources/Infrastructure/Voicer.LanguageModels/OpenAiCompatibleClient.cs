using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voicer.Core.Summaries;
using Voicer.Localization;

namespace Voicer.LanguageModels;

/// <summary>
/// <see cref="ILanguageModelClient"/> over the OpenAI-compatible API: <c>GET models</c> and a streamed
/// <c>POST chat/completions</c>. Everything the server sends is untrusted: it is read defensively and its failures are
/// worded for the user.
/// </summary>
public sealed class OpenAiCompatibleClient : ILanguageModelClient
{
    /// <summary>
    /// How long the list of models may take: a server that is up answers it at once.
    /// </summary>
    private static readonly TimeSpan _listTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// The prefix of a line of a server-sent event that carries data.
    /// </summary>
    private const string DATA_PREFIX = "data:";

    /// <summary>
    /// The data that ends a stream of the OpenAI API.
    /// </summary>
    private const string DONE = "[DONE]";

    /// <summary>
    /// How much of the text of a failure of the server is shown.
    /// </summary>
    private const int MAX_ERROR_LENGTH = 300;

    /// <summary>
    /// How random the answer is: low, so a summary keeps to the text.
    /// </summary>
    private const double TEMPERATURE = 0.3;

    /// <summary>
    /// The client of HTTP; it lives as long as the application and has no timeout of its own.
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatibleClient"/> class.
    /// </summary>
    /// <param name="http">The client of HTTP, without a timeout: an answer may take minutes.</param>
    public OpenAiCompatibleClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListModelsAsync(LanguageModelEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_listTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint.Resolve("models"));
        Authorize(request, endpoint);

        try
        {
            using var response = await SendAsync(request, endpoint, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            return ParseModels(body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw LocalizedError.Create(m => new HttpRequestException(m), "Summary.Unreachable", endpoint.BaseAddress);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedAsync(LanguageModelEndpoint endpoint, string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return [];
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["input"] = new JsonArray([.. inputs.Select(text => (JsonNode?)JsonValue.Create(text))]),
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Resolve("embeddings"))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        Authorize(request, endpoint);

        using var response = await SendAsync(request, endpoint, HttpCompletionOption.ResponseContentRead, cancellationToken);

        return ParseEmbeddings(await response.Content.ReadAsStringAsync(cancellationToken), inputs.Count);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        LanguageModelEndpoint endpoint,
        string model,
        IReadOnlyList<ChatMessage> messages,
        IProgress<string>? answerSoFar,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(messages);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Resolve("chat/completions"))
        {
            Content = new StringContent(RequestBody(model, messages), Encoding.UTF8, "application/json"),
        };
        Authorize(request, endpoint);

        using var response = await SendAsync(request, endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // A server that does not stream answers with one JSON document.
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            var answer = ParseMessage(await response.Content.ReadAsStringAsync(cancellationToken));
            answerSoFar?.Report(answer);

            return answer;
        }

        return await ReadStreamAsync(response, answerSoFar, cancellationToken);
    }

    /// <summary>
    /// Reads the names of the models from the answer to <c>GET models</c>.
    /// </summary>
    /// <param name="body">The answer.</param>
    /// <returns>The identifiers, sorted, without repeats.</returns>
    /// <exception cref="InvalidDataException">The answer is not a list of models.</exception>
    internal static IReadOnlyList<string> ParseModels(string body)
    {
        try
        {
            if (Field(JsonNode.Parse(body), "data") is not JsonArray data)
            {
                throw BadAnswer();
            }

            return [.. data
                .Select(item => Field(item, "id") is JsonValue id && id.TryGetValue<string>(out var name) ? name.Trim() : null)
                .OfType<string>()
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (JsonException)
        {
            throw BadAnswer();
        }
    }

    /// <summary>
    /// Reads the vectors from the answer to <c>POST embeddings</c>, put in the order of the texts by their index.
    /// </summary>
    /// <param name="body">The answer.</param>
    /// <param name="count">How many texts were sent.</param>
    /// <returns>A vector per text.</returns>
    /// <exception cref="InvalidDataException">The answer is not a vector per text of one length.</exception>
    internal static IReadOnlyList<float[]> ParseEmbeddings(string body, int count)
    {
        try
        {
            if (Field(JsonNode.Parse(body), "data") is not JsonArray data || data.Count != count)
            {
                throw BadAnswer();
            }

            var vectors = new float[count][];
            for (var i = 0; i < data.Count; i++)
            {
                var item = data[i];
                var index = Field(item, "index") is JsonValue position && position.TryGetValue<int>(out var at) ? at : i;
                if (item is not JsonObject || index < 0 || index >= count || vectors[index] is not null ||
                    Field(item, "embedding") is not JsonArray numbers || numbers.Count == 0)
                {
                    throw BadAnswer();
                }

                vectors[index] = [.. numbers.Select(n => n is JsonValue v && v.TryGetValue<double>(out var x) && double.IsFinite(x) ? (float)x : throw BadAnswer())];
            }

            if (vectors.Any(v => v.Length != vectors[0].Length))
            {
                throw BadAnswer();
            }

            return vectors;
        }
        catch (JsonException)
        {
            throw BadAnswer();
        }
    }

    /// <summary>
    /// Reads a piece of a streamed answer: its text, <c>choices[0].delta.content</c>, and the reasoning some servers
    /// send apart, <c>choices[0].delta.reasoning_content</c>.
    /// </summary>
    /// <param name="data">The data of one server-sent event.</param>
    /// <returns>The text and the reasoning; empty when the piece carries none (a role, the end).</returns>
    /// <exception cref="InvalidDataException">The piece is not JSON.</exception>
    /// <exception cref="HttpRequestException">The piece carries a failure of the server.</exception>
    internal static (string Content, string Reasoning) ParseDelta(string data)
    {
        try
        {
            var node = JsonNode.Parse(data);
            if (Field(node, "error") is { } error)
            {
                throw ServerError(500, ErrorText(error));
            }

            var delta = Field(First(Field(node, "choices")), "delta");

            return (Text(Field(delta, "content")), Text(Field(delta, "reasoning_content")));
        }
        catch (JsonException)
        {
            throw BadAnswer();
        }
    }

    /// <summary>
    /// Reads the text of an answer that came whole: <c>choices[0].message.content</c>, or its reasoning when the server
    /// put everything there (a model that never closed its reasoning).
    /// </summary>
    /// <param name="body">The answer.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The answer is not a completion.</exception>
    internal static string ParseMessage(string body)
    {
        try
        {
            var message = Field(First(Field(JsonNode.Parse(body), "choices")), "message") as JsonObject ?? throw BadAnswer();
            var content = Text(message["content"]);

            return content.Length > 0 ? content : Text(message["reasoning_content"]);
        }
        catch (JsonException)
        {
            throw BadAnswer();
        }
    }

    /// <summary>
    /// Writes the body of the request of a completion.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="messages">The conversation.</param>
    /// <returns>The JSON.</returns>
    internal static string RequestBody(string model, IReadOnlyList<ChatMessage> messages)
    {
        var conversation = new JsonArray();
        foreach (var message in messages)
        {
            conversation.Add(new JsonObject
            {
                ["role"] = message.Role.ToString().ToLowerInvariant(),
                ["content"] = message.Content,
            });
        }

        var body = new JsonObject
        {
            ["model"] = model,
            ["messages"] = conversation,
            ["stream"] = true,
            ["temperature"] = TEMPERATURE,
        };

        return body.ToJsonString();
    }

    /// <summary>
    /// Sends a request and turns a failure into a text for the user.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="endpoint">The server, for the text of the failure.</param>
    /// <param name="completion">When the answer counts as received.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The successful answer; the caller disposes it.</returns>
    /// <exception cref="HttpRequestException">The server cannot be reached or refused the request.</exception>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, LanguageModelEndpoint endpoint, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, completion, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw LocalizedError.Create(m => new HttpRequestException(m, ex), "Summary.Unreachable", endpoint.BaseAddress);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            string text;
            try
            {
                text = Field(JsonNode.Parse(body), "error") is { } error ? ErrorText(error) : body;
            }
            catch (JsonException)
            {
                text = body;
            }

            throw ServerError((int)response.StatusCode, text);
        }
    }

    /// <summary>
    /// Reads a stream of server-sent events and puts its pieces together.
    /// </summary>
    /// <param name="response">The answer of the server.</param>
    /// <param name="answerSoFar">Receives the answer received so far after each piece.</param>
    /// <param name="cancellationToken">Stops the reading.</param>
    /// <returns>The whole answer.</returns>
    private static async Task<string> ReadStreamAsync(HttpResponseMessage response, IProgress<string>? answerSoFar, CancellationToken cancellationToken)
    {
        var answer = new StringBuilder();
        var reasoning = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith(DATA_PREFIX, StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[DATA_PREFIX.Length..].Trim();
            if (data == DONE)
            {
                break;
            }

            var (content, thought) = ParseDelta(data);
            reasoning.Append(thought);
            if (content.Length > 0)
            {
                answer.Append(content);
                answerSoFar?.Report(answer.ToString());
            }
        }

        // A model that never closed its reasoning leaves its answer there: better shown than lost.
        if (answer.Length == 0 && reasoning.Length > 0)
        {
            answerSoFar?.Report(reasoning.ToString());

            return reasoning.ToString();
        }

        return answer.ToString();
    }

    /// <summary>
    /// Reads a field of an object of an answer; an answer of another shape has none, so a strange server is refused
    /// with a clear text instead of an error of the program.
    /// </summary>
    /// <param name="node">The object, or anything else the server sent.</param>
    /// <param name="name">The name of the field.</param>
    /// <returns>The field, or <see langword="null"/> when the node is not an object or has no such field.</returns>
    private static JsonNode? Field(JsonNode? node, string name)
    {
        return node is JsonObject item ? item[name] : null;
    }

    /// <summary>
    /// Reads the first item of a list of an answer.
    /// </summary>
    /// <param name="node">The list, or anything else the server sent.</param>
    /// <returns>The first item, or <see langword="null"/> when the node is not a list or is empty.</returns>
    private static JsonNode? First(JsonNode? node)
    {
        return node is JsonArray list && list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// Reads a text field of an answer.
    /// </summary>
    /// <param name="node">The field, or <see langword="null"/>.</param>
    /// <returns>Its text; empty when it is missing or not a text.</returns>
    private static string Text(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
    }

    /// <summary>
    /// Adds the key of the server to a request, when there is one.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="endpoint">The server.</param>
    private static void Authorize(HttpRequestMessage request, LanguageModelEndpoint endpoint)
    {
        if (endpoint.ApiKey is { } key)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

    /// <summary>
    /// Reads the text of an <c>error</c> of the API: an object with a message, or a text.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The text.</returns>
    private static string ErrorText(JsonNode error)
    {
        if (Field(error, "message") is JsonValue message && message.TryGetValue<string>(out var text))
        {
            return text;
        }

        return error is JsonValue value && value.TryGetValue<string>(out var plain) ? plain : error.ToJsonString();
    }

    /// <summary>
    /// Words a refusal of the server.
    /// </summary>
    /// <param name="status">The status of the answer.</param>
    /// <param name="text">What the server said.</param>
    /// <returns>The exception.</returns>
    private static HttpRequestException ServerError(int status, string text)
    {
        var shown = text.Trim();
        if (shown.Length > MAX_ERROR_LENGTH)
        {
            shown = shown[..MAX_ERROR_LENGTH] + "…";
        }

        return LocalizedError.Create(m => new HttpRequestException(m, null, (System.Net.HttpStatusCode)status), "Summary.ServerError", status, shown);
    }

    /// <summary>
    /// Words an answer that cannot be read.
    /// </summary>
    /// <returns>The exception.</returns>
    private static InvalidDataException BadAnswer()
    {
        return LocalizedError.Create(m => new InvalidDataException(m), "Summary.BadAnswer");
    }
}
