using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// <see cref="IModelSourceStore"/> in the settings of the user, under a key of its own. Before the first choice the
/// source follows what is already there, so an update changes nothing, and that first guess is remembered.
/// </summary>
public abstract class ModelSourceStore : IModelSourceStore
{
    /// <summary>
    /// The settings of the user.
    /// </summary>
    private readonly ISettingsStore _settings;

    /// <summary>
    /// The key of the choice in the settings.
    /// </summary>
    private readonly string _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSourceStore"/> class.
    /// </summary>
    /// <param name="settings">The settings of the user.</param>
    /// <param name="key">The key of the choice in the settings.</param>
    protected ModelSourceStore(ISettingsStore settings, string key)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
    }

    /// <inheritdoc />
    public ModelSource Load()
    {
        if (Enum.TryParse<ModelSource>(_settings.Get(_key), ignoreCase: false, out var source) && Enum.IsDefined(source))
        {
            return source;
        }

        // The guess is remembered at once: what the settings show stays what is used until the user chooses.
        var guess = Guess();
        Save(guess);

        return guess;
    }

    /// <inheritdoc />
    public void Save(ModelSource source)
    {
        _settings.Set(_key, source.ToString());
    }

    /// <summary>
    /// Finds the source before the first choice, from the models installed and the settings of the server.
    /// </summary>
    /// <returns>The source.</returns>
    protected abstract ModelSource Guess();
}
