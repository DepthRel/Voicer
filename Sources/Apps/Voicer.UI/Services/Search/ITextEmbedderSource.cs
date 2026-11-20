using Voicer.Core.Search;

namespace Voicer.UI.Services.Search;

/// <summary>
/// Chooses the model of the search by meaning from what is installed and set up now.
/// </summary>
public interface ITextEmbedderSource
{
    /// <summary>
    /// Gives the model of the search by meaning.
    /// </summary>
    /// <returns>The model, or <see langword="null"/> when the history is searched by words only.</returns>
    ITextEmbedder? Current();
}
