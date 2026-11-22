namespace Voicer.UI.Enums;

/// <summary>
/// What runs a model the application needs (the model of the search by meaning, the model of the summaries), as the
/// user chose in the settings.
/// </summary>
public enum ModelSource
{
    /// <summary>
    /// Nothing: the feature is off (the history is searched by words only, no summaries are written).
    /// </summary>
    Off,

    /// <summary>
    /// A downloaded model of Voicer, run by its own llama.cpp.
    /// </summary>
    Local,

    /// <summary>
    /// A model of the server of language models the user connects to.
    /// </summary>
    Server,
}
