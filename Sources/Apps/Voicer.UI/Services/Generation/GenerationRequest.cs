using Voicer.Core.Synthesis;

namespace Voicer.UI.Services.Generation;

/// <summary>
/// What a generation of speech is asked to do.
/// </summary>
/// <param name="Text">The text to speak.</param>
/// <param name="Voice">How it is spoken; its sample, when there is one, is the file the user chose.</param>
/// <param name="DeviceName">The name of the device as it is shown, kept with the record.</param>
public sealed record GenerationRequest(string Text, SynthesisVoice Voice, string DeviceName);
