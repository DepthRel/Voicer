namespace Voicer.Core.History;

/// <summary>
/// The compressed sound of a recognized file, ready to be kept with its text.
/// </summary>
/// <param name="FilePath">The file of the sound (Ogg Opus).</param>
/// <param name="Duration">How long the sound lasts.</param>
public sealed record RecordAudio(string FilePath, TimeSpan Duration);
