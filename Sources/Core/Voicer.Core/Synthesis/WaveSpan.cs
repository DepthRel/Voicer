namespace Voicer.Core.Synthesis;

/// <summary>
/// Where one piece of sound lies in a joined recording.
/// </summary>
/// <param name="Start">Where the piece starts.</param>
/// <param name="End">Where the piece ends.</param>
public sealed record WaveSpan(TimeSpan Start, TimeSpan End);
