namespace Voicer.Core.Summaries;

/// <summary>
/// Where a server of language models with the OpenAI-compatible API answers (LM Studio, Ollama, llama.cpp, Jan…).
/// </summary>
/// <param name="BaseAddress">The address of the API, up to and with its version: <c>http://localhost:1234/v1</c>.</param>
/// <param name="ApiKey">The key the server asks for, or <see langword="null"/> for a local server without one.</param>
public sealed record LanguageModelEndpoint(Uri BaseAddress, string? ApiKey)
{
    /// <summary>
    /// Reads an address the user typed.
    /// </summary>
    /// <param name="address">The address; only absolute <c>http</c> and <c>https</c> addresses are taken.</param>
    /// <param name="apiKey">The key, or an empty text for none.</param>
    /// <param name="endpoint">The endpoint, when the address is valid.</param>
    /// <returns><see langword="true"/> when the address is valid.</returns>
    public static bool TryCreate(string? address, string? apiKey, out LanguageModelEndpoint endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(address?.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        endpoint = new LanguageModelEndpoint(uri, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim());

        return true;
    }

    /// <summary>
    /// Builds the address of a method of the API.
    /// </summary>
    /// <param name="path">The path of the method under the base address: <c>models</c>, <c>chat/completions</c>.</param>
    /// <returns>The full address.</returns>
    public Uri Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new Uri(BaseAddress.AbsoluteUri.TrimEnd('/') + "/" + path.TrimStart('/'));
    }
}
