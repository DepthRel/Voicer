using FluentAssertions;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the cutting of a long text into parts a language model reads at once.
/// </summary>
public sealed class TextChunkerTests
{
    /// <summary>
    /// A text that fits stays whole.
    /// </summary>
    [Fact]
    public void Split_KeepsAShortTextWhole()
    {
        TextChunker.Split("One.\nTwo.", 100).Should().Equal("One.\nTwo.");
    }

    /// <summary>
    /// Lines are kept together as long as they fit, and never cut when they fit alone.
    /// </summary>
    [Fact]
    public void Split_PacksWholeLines()
    {
        var parts = TextChunker.Split("aaaa\nbbbb\ncccc\ndddd", 9);

        parts.Should().Equal("aaaa\nbbbb", "cccc\ndddd");
    }

    /// <summary>
    /// A line that is too long is cut after its sentences, a sentence that is too long between its words, and a word
    /// that is too long anywhere; no part is longer than the limit and nothing is lost.
    /// </summary>
    [Fact]
    public void Split_CutsLongLinesBySentencesThenWords()
    {
        var text = "First sentence here. Second one is a bit longer than the limit allows. Third.\nSupercalifragilisticexpialidocious";

        var parts = TextChunker.Split(text, 24);

        parts.Should().OnlyContain(p => p.Length <= 24 && p.Length > 0);
        parts[0].Should().Be("First sentence here.");
        string.Concat(parts).Replace(" ", string.Empty).Replace("\n", string.Empty)
            .Should().Be(text.Replace(" ", string.Empty).Replace("\n", string.Empty));
    }

    /// <summary>
    /// Empty lines give no empty parts.
    /// </summary>
    [Fact]
    public void Split_SkipsEmptyLines()
    {
        TextChunker.Split("\n\n  \nText.\n\n", 50).Should().Equal("Text.");
        TextChunker.Split(string.Empty, 50).Should().BeEmpty();
    }
}
