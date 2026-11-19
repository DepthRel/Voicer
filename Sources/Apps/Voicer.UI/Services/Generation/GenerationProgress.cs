using Voicer.UI.Enums;

namespace Voicer.UI.Services.Generation;

/// <summary>
/// The progress of a generation of speech.
/// </summary>
/// <param name="Stage">What the generation is doing.</param>
/// <param name="Part">The piece of the text being spoken, from 1; 0 outside of speaking.</param>
/// <param name="Parts">How many pieces the text has.</param>
public sealed record GenerationProgress(GenerationStage Stage, int Part, int Parts)
{
    /// <summary>
    /// Gets how much of the generation is done, from 0 to 100: the pieces already spoken.
    /// </summary>
    public double Percent => Parts <= 0 ? 0 : Math.Clamp(100.0 * Math.Max(Part - 1, 0) / Parts, 0, 100);
}
