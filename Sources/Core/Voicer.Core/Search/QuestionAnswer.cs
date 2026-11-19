namespace Voicer.Core.Search;

/// <summary>
/// The answer of a language model to a question about the records, with the passages it was given.
/// </summary>
/// <param name="Answer">The answer; its references <c>[1]</c>, <c>[2]</c>… point at the sources by their number.</param>
/// <param name="Sources">The passages the answer is based on, numbered from 1 in this order.</param>
public sealed record QuestionAnswer(string Answer, IReadOnlyList<PassageHit> Sources);
