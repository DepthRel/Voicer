namespace Voicer.Core.Enums;

/// <summary>
/// Who speaks in a message of a conversation with a language model.
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// The instructions the model follows throughout.
    /// </summary>
    System,

    /// <summary>
    /// The request of the application.
    /// </summary>
    User,

    /// <summary>
    /// The answer of the model.
    /// </summary>
    Assistant,
}
