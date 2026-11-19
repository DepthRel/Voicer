namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="IGenerationPreferencesStore"/> in the settings of the data folder (<c>Data/settings.json</c>).
/// </summary>
public sealed class GenerationPreferencesStore : IGenerationPreferencesStore
{
    /// <summary>
    /// The setting of the voice model.
    /// </summary>
    private const string MODEL = "generation.model";

    /// <summary>
    /// The setting of the language.
    /// </summary>
    private const string LANGUAGE = "generation.language";

    /// <summary>
    /// The setting of the voice shipped with the model.
    /// </summary>
    private const string VOICE = "generation.voice";

    /// <summary>
    /// The setting of the sample of the voice.
    /// </summary>
    private const string SAMPLE = "generation.sample";

    /// <summary>
    /// The settings of the user.
    /// </summary>
    private readonly ISettingsStore _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationPreferencesStore"/> class.
    /// </summary>
    /// <param name="settings">The settings of the user.</param>
    public GenerationPreferencesStore(ISettingsStore settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public GenerationPreferences Load()
    {
        return new GenerationPreferences(
            Read(MODEL),
            Read(LANGUAGE),
            Read(VOICE),
            Read(SAMPLE));
    }

    /// <inheritdoc />
    public void Save(GenerationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        _settings.Set(MODEL, preferences.ModelId ?? string.Empty);
        _settings.Set(LANGUAGE, preferences.Language ?? string.Empty);
        _settings.Set(VOICE, preferences.Voice ?? string.Empty);
        _settings.Set(SAMPLE, preferences.SamplePath ?? string.Empty);
    }

    /// <summary>
    /// Reads a setting; an empty value is no value.
    /// </summary>
    /// <param name="key">The name of the setting.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    private string? Read(string key)
    {
        var value = _settings.Get(key);

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
