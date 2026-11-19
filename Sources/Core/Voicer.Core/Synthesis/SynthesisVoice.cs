using Voicer.Core.Devices;

namespace Voicer.Core.Synthesis;

/// <summary>
/// How a text is spoken: the model, its language and voice, and the device it runs on.
/// </summary>
/// <param name="Model">The voice model.</param>
/// <param name="Language">The language of the text, as a code; passed to the model only when it speaks several.</param>
/// <param name="Voice">A voice shipped with the model, or <see langword="null"/> for its default one.</param>
/// <param name="SamplePath">A WAV recording of the voice to speak in, or <see langword="null"/>.</param>
/// <param name="Device">The device the model runs on.</param>
public sealed record SynthesisVoice(VoiceModel Model, string Language, string? Voice, string? SamplePath, ComputeDevice Device);
