namespace Voicer.UI.Enums;

/// <summary>
/// What makes the vectors of the search by meaning, as the user chose in the settings.
/// </summary>
public enum SearchSource
{
    /// <summary>
    /// Nothing: the history is searched by words only.
    /// </summary>
    Off,

    /// <summary>
    /// A downloaded model of Voicer, run by its own llama.cpp.
    /// </summary>
    Local,

    /// <summary>
    /// A model of embeddings of the server of language models the user runs.
    /// </summary>
    Server,
}
