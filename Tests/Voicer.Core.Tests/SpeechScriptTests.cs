using FluentAssertions;
using Voicer.Core.Synthesis;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the cutting of a script into the pieces a voice model speaks at once.
/// </summary>
public sealed class SpeechScriptTests
{
    /// <summary>
    /// Every line is a paragraph; the last piece of a paragraph says so, for the longer pause after it.
    /// </summary>
    [Fact]
    public void Lines_AreParagraphs()
    {
        var parts = SpeechScript.Split("First paragraph.\r\n\r\nSecond one.\nThird one.");

        parts.Should().Equal(
            new ScriptPart("First paragraph.", true),
            new ScriptPart("Second one.", true),
            new ScriptPart("Third one.", true));
    }

    /// <summary>
    /// A long paragraph is cut at the ends of its sentences, never in a word, and its pieces fit.
    /// </summary>
    [Fact]
    public void LongParagraph_IsCutBetweenSentences()
    {
        var parts = SpeechScript.Split("One two three. Four five six. Seven eight nine.", 30);

        parts.Select(p => p.Text).Should().Equal("One two three. Four five six.", "Seven eight nine.");
        parts.Select(p => p.EndsParagraph).Should().Equal(false, true);
        parts.Should().OnlyContain(p => p.Text.Length <= 30);
    }

    /// <summary>
    /// The spaces of a piece are made single, so the model reads one line.
    /// </summary>
    [Fact]
    public void Spaces_AreMadeSingle()
    {
        SpeechScript.Split("  Hello,\t  world!  ").Should().Equal(new ScriptPart("Hello, world!", true));
    }

    /// <summary>
    /// A text of spaces has nothing to speak.
    /// </summary>
    [Fact]
    public void Blank_HasNoParts()
    {
        SpeechScript.Split(" \n\t \r\n ").Should().BeEmpty();
    }
}
