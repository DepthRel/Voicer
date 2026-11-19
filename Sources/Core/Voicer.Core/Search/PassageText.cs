namespace Voicer.Core.Search;

/// <summary>
/// A passage that has no vector of a model yet.
/// </summary>
/// <param name="PassageId">The identifier of the passage.</param>
/// <param name="Text">Its text.</param>
public sealed record PassageText(long PassageId, string Text);
