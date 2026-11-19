namespace Voicer.Core.Synthesis;

/// <summary>
/// A piece of a text the voice model speaks at once: one or more whole sentences of one paragraph.
/// </summary>
/// <param name="Text">The text of the piece, on one line.</param>
/// <param name="EndsParagraph">Whether the piece is the last one of its paragraph, so a longer pause follows it.</param>
public sealed record ScriptPart(string Text, bool EndsParagraph);
