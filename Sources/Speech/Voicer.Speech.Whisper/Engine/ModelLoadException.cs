namespace Voicer.Speech.Whisper.Engine;

/// <summary>
/// The speech engine could not load a model on the chosen device.
/// </summary>
public sealed class ModelLoadException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelLoadException"/> class.
    /// </summary>
    public ModelLoadException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelLoadException"/> class.
    /// </summary>
    /// <param name="message">What went wrong, in English.</param>
    public ModelLoadException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelLoadException"/> class.
    /// </summary>
    /// <param name="message">What went wrong, in English.</param>
    /// <param name="innerException">The failure of the engine.</param>
    public ModelLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
