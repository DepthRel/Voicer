using Voicer.UI.Enums;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// How far a recognition got.
/// </summary>
/// <param name="Stage">What the recognition is doing.</param>
/// <param name="Percent">How much of the stage is done, from 0 to 100.</param>
public sealed record TranscriptionProgress(TranscriptionStage Stage, double Percent);
