namespace Voicer.UI.Services.History;

/// <summary>
/// A recognized text kept in the history.
/// </summary>
/// <param name="Id">The identifier of the record.</param>
/// <param name="HasAudio">Whether its sound is kept too.</param>
/// <param name="AudioFailure">Why the sound could not be kept, or <see langword="null"/>.</param>
public sealed record KeptRecord(long Id, bool HasAudio, Exception? AudioFailure);
