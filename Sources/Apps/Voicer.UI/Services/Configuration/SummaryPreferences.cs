using Voicer.Core.Search;
using Voicer.Core.Summaries;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// What the user chose for summaries and the search by meaning, as typed: it may be incomplete or invalid until it is used.
/// </summary>
/// <param name="Address">The address of the API of the server.</param>
/// <param name="ApiKey">The key of the server, or an empty text.</param>
/// <param name="Model">The identifier of the model, or an empty text.</param>
/// <param name="ContextTokens">The context of the model, in tokens.</param>
/// <param name="EmbeddingModel">The identifier of the model of embeddings of the search by meaning, or an empty text.</param>
public sealed record SummaryPreferences(string Address, string ApiKey, string Model, int ContextTokens, string EmbeddingModel)
{
    /// <summary>
    /// Gets the choice of a user who has not chosen anything yet: LM Studio on this computer, no model.
    /// </summary>
    public static SummaryPreferences Default { get; } = new(LanguageModelPreset.All[0].Address, string.Empty, string.Empty, SummarySettings.DEFAULT_CONTEXT_TOKENS, string.Empty);

    /// <summary>
    /// Turns the choice into settings a summary can be made with.
    /// </summary>
    /// <returns>The settings, or <see langword="null"/> while the address is invalid or no model is chosen.</returns>
    public SummarySettings? ToSettings()
    {
        if (string.IsNullOrWhiteSpace(Model) || !LanguageModelEndpoint.TryCreate(Address, ApiKey, out var endpoint))
        {
            return null;
        }

        var context = Math.Clamp(ContextTokens, SummarySettings.MIN_CONTEXT_TOKENS, SummarySettings.MAX_CONTEXT_TOKENS);

        return new SummarySettings(endpoint, Model.Trim(), context);
    }

    /// <summary>
    /// Turns the choice into settings of the search by meaning.
    /// </summary>
    /// <returns>The settings, or <see langword="null"/> while the address is invalid or no model of embeddings is chosen.</returns>
    public EmbeddingSettings? ToEmbeddingSettings()
    {
        if (string.IsNullOrWhiteSpace(EmbeddingModel) || !LanguageModelEndpoint.TryCreate(Address, ApiKey, out var endpoint))
        {
            return null;
        }

        return new EmbeddingSettings(endpoint, EmbeddingModel.Trim());
    }
}
