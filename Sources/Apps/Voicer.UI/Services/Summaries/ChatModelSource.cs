using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Servers;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Services.Summaries;

/// <summary>
/// <see cref="IChatModelSource"/> over the choice of the user: the server of language models from its settings, or the
/// first downloaded model of Voicer in its llama-server.
/// </summary>
public sealed class ChatModelSource : IChatModelSource
{
    /// <summary>
    /// The choice of the source.
    /// </summary>
    private readonly ISummarySourceStore _source;

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IChatModelStore _models;

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _llamaCpp;

    /// <summary>
    /// Runs a model of Voicer.
    /// </summary>
    private readonly ILlamaModelServer<ChatModel> _server;

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatModelSource"/> class.
    /// </summary>
    /// <param name="source">The choice of the source.</param>
    /// <param name="models">Where the models of Voicer live.</param>
    /// <param name="llamaCpp">Finds llama-server.</param>
    /// <param name="server">Runs a model of Voicer.</param>
    /// <param name="preferences">The settings of the server of language models.</param>
    public ChatModelSource(
        ISummarySourceStore source,
        IChatModelStore models,
        ILlamaCppLocator llamaCpp,
        ILlamaModelServer<ChatModel> server,
        ISummaryPreferencesStore preferences)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _models = models ?? throw new ArgumentNullException(nameof(models));
        _llamaCpp = llamaCpp ?? throw new ArgumentNullException(nameof(llamaCpp));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    /// <inheritdoc />
    public bool IsAvailable => _source.Load() switch
    {
        ModelSource.Server => _preferences.Load().ToSettings() is not null,
        ModelSource.Local => _models.Preferred() is not null && _llamaCpp.LocateServer() is not null,
        _ => false,
    };

    /// <inheritdoc />
    public async Task<ChatSession> OpenAsync(CancellationToken cancellationToken)
    {
        var source = _source.Load();
        if (source == ModelSource.Server && _preferences.Load().ToSettings() is { } settings)
        {
            return new ChatSession(settings);
        }

        if (source == ModelSource.Local && _models.Preferred() is { } model)
        {
            var lease = await _server.AcquireAsync(model, cancellationToken);

            return new ChatSession(new SummarySettings(lease.Endpoint, model.Id, model.ContextTokens), model.Name, lease);
        }

        throw LocalizedError.Create(m => new InvalidOperationException(m), "Summary.NotConfigured");
    }
}
