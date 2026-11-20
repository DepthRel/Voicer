namespace Voicer.UI.Services.History;

/// <summary>
/// What a record is opened at from the search: the found passage, played from its start, shown marked and scrolled to,
/// with the words of the query marked in it.
/// </summary>
/// <param name="Start">Where the passage starts in the recording.</param>
/// <param name="End">Where it ends.</param>
/// <param name="Query">What the user looked for.</param>
public sealed record RecordFocus(TimeSpan Start, TimeSpan End, string Query);
