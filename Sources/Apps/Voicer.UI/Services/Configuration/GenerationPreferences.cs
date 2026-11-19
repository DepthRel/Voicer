namespace Voicer.UI.Services.Configuration;

/// <summary>
/// The choices of the Generation tab the application remembers, so a series of recordings keeps one voice.
/// </summary>
/// <param name="ModelId">The identifier of the voice model, or <see langword="null"/>.</param>
/// <param name="Language">The language of the text, as a code, or <see langword="null"/>.</param>
/// <param name="Voice">The voice shipped with the model, or <see langword="null"/>.</param>
/// <param name="SamplePath">The file with a sample of the voice, or <see langword="null"/>.</param>
public sealed record GenerationPreferences(string? ModelId, string? Language, string? Voice, string? SamplePath)
{
    /// <summary>
    /// Gets the choices of a first start: nothing chosen.
    /// </summary>
    public static GenerationPreferences Empty { get; } = new(null, null, null, null);
}
