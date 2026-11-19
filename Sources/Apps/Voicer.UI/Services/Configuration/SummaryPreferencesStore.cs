using System.Globalization;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="ISummaryPreferencesStore"/> in the settings of the data folder (<c>Data/settings.json</c>).
/// </summary>
public sealed class SummaryPreferencesStore : ISummaryPreferencesStore
{
    /// <summary>
    /// The setting of the address of the API.
    /// </summary>
    private const string ADDRESS = "summary.address";

    /// <summary>
    /// The setting of the key of the server.
    /// </summary>
    private const string API_KEY = "summary.apiKey";

    /// <summary>
    /// The setting of the model.
    /// </summary>
    private const string MODEL = "summary.model";

    /// <summary>
    /// The setting of the context of the model.
    /// </summary>
    private const string CONTEXT = "summary.contextTokens";

    /// <summary>
    /// The setting of the model of embeddings.
    /// </summary>
    private const string EMBEDDING_MODEL = "summary.embeddingModel";

    /// <summary>
    /// The settings of the user.
    /// </summary>
    private readonly ISettingsStore _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummaryPreferencesStore"/> class.
    /// </summary>
    /// <param name="settings">The settings of the user.</param>
    public SummaryPreferencesStore(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public SummaryPreferences Load()
    {
        var defaults = SummaryPreferences.Default;
        var context = int.TryParse(_settings.Get(CONTEXT), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokens)
            ? tokens
            : defaults.ContextTokens;

        return new SummaryPreferences(
            _settings.Get(ADDRESS) ?? defaults.Address,
            _settings.Get(API_KEY) ?? defaults.ApiKey,
            _settings.Get(MODEL) ?? defaults.Model,
            context,
            _settings.Get(EMBEDDING_MODEL) ?? defaults.EmbeddingModel);
    }

    /// <inheritdoc />
    public void Save(SummaryPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        _settings.Set(ADDRESS, preferences.Address);
        _settings.Set(API_KEY, preferences.ApiKey);
        _settings.Set(MODEL, preferences.Model);
        _settings.Set(CONTEXT, preferences.ContextTokens.ToString(CultureInfo.InvariantCulture));
        _settings.Set(EMBEDDING_MODEL, preferences.EmbeddingModel);
    }
}
