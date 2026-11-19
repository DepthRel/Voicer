namespace Voicer.Core.Summaries;

/// <summary>
/// What a summary is made with: the server, the model and how much text the model reads at once.
/// </summary>
/// <param name="Endpoint">The server of language models.</param>
/// <param name="Model">The identifier of the model on the server.</param>
/// <param name="ContextTokens">
/// The context the model is loaded with, in tokens; a longer text is summarized part by part.
/// </param>
public sealed record SummarySettings(LanguageModelEndpoint Endpoint, string Model, int ContextTokens)
{
    /// <summary>
    /// The smallest context that is taken.
    /// </summary>
    public const int MIN_CONTEXT_TOKENS = 2048;

    /// <summary>
    /// The largest context that is taken.
    /// </summary>
    public const int MAX_CONTEXT_TOKENS = 262144;

    /// <summary>
    /// The context assumed until the user says otherwise: what local servers load most models with.
    /// </summary>
    public const int DEFAULT_CONTEXT_TOKENS = 8192;
}
