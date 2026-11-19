namespace Voicer.Core.History;

/// <summary>
/// The summary of a recognized text, kept with its record.
/// </summary>
/// <param name="Text">The summary, as the model wrote it.</param>
/// <param name="ModelName">The model that wrote it.</param>
/// <param name="CreatedAt">When it was written.</param>
public sealed record RecordSummary(string Text, string ModelName, DateTimeOffset CreatedAt);
