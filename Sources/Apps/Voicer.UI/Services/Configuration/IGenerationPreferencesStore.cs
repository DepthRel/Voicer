namespace Voicer.UI.Services.Configuration;

/// <summary>
/// Keeps the choices of the Generation tab.
/// </summary>
public interface IGenerationPreferencesStore
{
    /// <summary>
    /// Reads the choices.
    /// </summary>
    /// <returns>The kept choices; nothing chosen when none are kept.</returns>
    GenerationPreferences Load();

    /// <summary>
    /// Keeps the choices.
    /// </summary>
    /// <param name="preferences">The choices.</param>
    /// <exception cref="IOException">The settings could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The settings could not be written.</exception>
    void Save(GenerationPreferences preferences);
}
