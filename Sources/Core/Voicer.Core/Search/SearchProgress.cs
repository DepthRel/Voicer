namespace Voicer.Core.Search;

/// <summary>
/// How far the indexing of the passages for the search by meaning is.
/// </summary>
/// <param name="Done">How many passages have their vector.</param>
/// <param name="Total">How many passages needed one when the search started.</param>
public sealed record SearchProgress(int Done, int Total);
