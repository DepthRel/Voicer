namespace Voicer.Core.Synthesis;

/// <summary>
/// A text-to-speech model the application can download and run with llama.cpp: the weights of the language model that
/// speaks and the projector that turns its output into sound (the audio codec, the voices and the speaker encoder).
/// </summary>
/// <param name="Id">The identifier of the model; it is also the name of its folder in <c>Data/Models</c>, so it holds only Latin letters, digits and dashes.</param>
/// <param name="Name">The name of the model as it is shown; a product name, the same in every language.</param>
/// <param name="Weights">The file of the weights.</param>
/// <param name="Projector">The file of the projector (<c>mmproj</c>).</param>
/// <param name="Languages">
/// The languages the model speaks, as codes (<c>en</c>, <c>ru</c>), the default one first; with a single language the
/// model is not told which one it is.
/// </param>
/// <param name="Voices">The voices shipped with the model, by the names the engine takes; empty when it has one built-in voice or none.</param>
/// <param name="CanCloneVoice">Whether the model speaks in the voice of a sample recording.</param>
/// <param name="NeedsSample">Whether the model speaks well only with a sample of the voice.</param>
/// <param name="RunsOnCpu">Whether the model works at a usable speed on the processor alone, without a video card.</param>
public sealed record VoiceModel(
    string Id,
    string Name,
    VoiceModelFile Weights,
    VoiceModelFile Projector,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Voices,
    bool CanCloneVoice,
    bool NeedsSample,
    bool RunsOnCpu)
{
    /// <summary>
    /// Gets the files of the model: the weights and the projector.
    /// </summary>
    public IReadOnlyList<VoiceModelFile> Files => [Weights, Projector];

    /// <summary>
    /// Gets the size of the whole download, in bytes.
    /// </summary>
    public long SizeBytes => Weights.SizeBytes + Projector.SizeBytes;

    /// <summary>
    /// Gets the key of the text that describes the model to the user.
    /// </summary>
    public string DescriptionKey => $"VoiceModel.{Id}.Description";

    /// <summary>
    /// Gets a value indicating whether the model speaks several languages, so the language is chosen and passed to it.
    /// </summary>
    public bool IsMultilingual => Languages.Count > 1;
}
