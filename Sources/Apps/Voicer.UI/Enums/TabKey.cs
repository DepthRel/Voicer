namespace Voicer.UI.Enums;

/// <summary>
/// The tabs of the main window, in their order.
/// </summary>
public enum TabKey
{
    /// <summary>
    /// Speech to text.
    /// </summary>
    Transcription,

    /// <summary>
    /// Text to speech.
    /// </summary>
    Generation,

    /// <summary>
    /// The recognized and generated speech kept in the database.
    /// </summary>
    History,

    /// <summary>
    /// The language, the models and the tools.
    /// </summary>
    Settings,
}
