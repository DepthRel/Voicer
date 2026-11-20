using Voicer.UI.Enums;

namespace Voicer.UI.Services.Configuration;

/// <summary>
/// Remembers what makes the vectors of the search by meaning.
/// </summary>
public interface ISearchSourceStore
{
    /// <summary>
    /// Reads the choice.
    /// </summary>
    /// <returns>The source chosen, or, before the first choice, the one the installed models and the settings suggest.</returns>
    SearchSource Load();

    /// <summary>
    /// Remembers a choice.
    /// </summary>
    /// <param name="source">The source.</param>
    void Save(SearchSource source);
}
