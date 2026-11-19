namespace Voicer.Core.Search;

/// <summary>
/// A passage of a recognized text: a few fragments of speech in a row, the unit the search finds and the model reads.
/// </summary>
/// <param name="Start">The moment of the record the passage starts at.</param>
/// <param name="End">The moment of the record the passage ends at.</param>
/// <param name="Text">The text of the passage.</param>
public sealed record Passage(TimeSpan Start, TimeSpan End, string Text);
