using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// Remembers what runs a model the application needs.
/// </summary>
public interface IModelSourceStore
{
    /// <summary>
    /// Reads the choice.
    /// </summary>
    /// <returns>The source chosen, or, before the first choice, the one the installed models and the settings suggest, which is remembered from then on.</returns>
    ModelSource Load();

    /// <summary>
    /// Remembers a choice.
    /// </summary>
    /// <param name="source">The source.</param>
    void Save(ModelSource source);
}
