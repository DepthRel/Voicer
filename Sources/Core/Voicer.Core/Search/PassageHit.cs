namespace Voicer.Core.Search;

/// <summary>
/// A passage the search found, with the record it belongs to.
/// </summary>
/// <param name="PassageId">The identifier of the passage.</param>
/// <param name="RecordId">The identifier of its record.</param>
/// <param name="SourceName">The name of the recognized file.</param>
/// <param name="CreatedAt">When the record was recognized.</param>
/// <param name="Start">The moment of the record the passage starts at.</param>
/// <param name="End">The moment of the record the passage ends at.</param>
/// <param name="Text">The text of the passage.</param>
public sealed record PassageHit(long PassageId, long RecordId, string SourceName, DateTimeOffset CreatedAt, TimeSpan Start, TimeSpan End, string Text);
