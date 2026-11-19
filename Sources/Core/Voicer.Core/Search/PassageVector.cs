namespace Voicer.Core.Search;

/// <summary>
/// The vector of the meaning of a passage, made by a model of embeddings.
/// </summary>
/// <param name="PassageId">The identifier of the passage.</param>
/// <param name="Vector">The vector, of unit length.</param>
public sealed record PassageVector(long PassageId, float[] Vector);
