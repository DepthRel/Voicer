namespace Voicer.Core.Summaries;

/// <summary>
/// How far a summary is.
/// </summary>
/// <param name="Step">The request being made, from 1.</param>
/// <param name="Steps">How many requests the summary takes: one per part of a long text, then the final one.</param>
/// <param name="Text">The final summary received so far, or <see langword="null"/> while the parts are read.</param>
public sealed record SummaryProgress(int Step, int Steps, string? Text);
