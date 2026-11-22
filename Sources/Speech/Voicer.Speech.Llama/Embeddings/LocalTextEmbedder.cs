using Voicer.Core.Embeddings;
using Voicer.Core.Search;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Servers;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// <see cref="ITextEmbedder"/> over a model of the application run by its own llama-server, started on demand.
/// </summary>
public sealed class LocalTextEmbedder : ITextEmbedder
{
    /// <summary>
    /// Runs the server of the model.
    /// </summary>
    private readonly ILlamaModelServer<EmbeddingModel> _server;

    /// <summary>
    /// Talks to the server.
    /// </summary>
    private readonly ILanguageModelClient _client;

    /// <summary>
    /// The model.
    /// </summary>
    private readonly EmbeddingModel _model;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTextEmbedder"/> class.
    /// </summary>
    /// <param name="server">Runs the server of the model.</param>
    /// <param name="client">Talks to the server.</param>
    /// <param name="model">The model.</param>
    public LocalTextEmbedder(ILlamaModelServer<EmbeddingModel> server, ILanguageModelClient client, EmbeddingModel model)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc />
    public string Model => _model.VectorName;

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        // A text longer than a slot is refused by the server: its beginning carries its meaning well enough.
        var fitted = texts.Select(text => text.Length <= _model.MaxTextLength ? text : text[.._model.MaxTextLength]).ToList();

        using var lease = await _server.AcquireAsync(_model, cancellationToken).ConfigureAwait(false);
        try
        {
            return await _client.EmbedAsync(lease.Endpoint, _model.Id, fitted, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            // No answer at all from a server of this computer: the process ended; the next search starts it again.
            throw LocalizedError.Create(m => new HttpRequestException(m, ex), "Search.ServerStopped", _model.Name);
        }
    }
}
