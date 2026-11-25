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

    /// <summary>
    /// The stops of scripts written without a space after them end sentences too: a Chinese line and a Hindi line are
    /// cut after their sentences, not in the middle of one.
    /// </summary>
    [Fact]
    public void Split_CutsAfterTheStopsOfOtherScripts()
    {
        TextChunker.Split("今天天气很好。我们去公园吧！你来吗？", 8).Should().Equal("今天天气很好。", "我们去公园吧！", "你来吗？");
        TextChunker.Split("यह पहला वाक्य है। यह दूसरा है।", 18).Should().Equal("यह पहला वाक्य है।", "यह दूसरा है।");
    }

    /// <summary>
    /// A run without spaces is cut between whole characters: a pair of surrogates or a letter with its marks is kept.
    /// </summary>
    [Fact]
    public void Split_NeverCutsACharacter()
    {
        var text = string.Concat(Enumerable.Repeat("𠀀", 5)) + string.Concat(Enumerable.Repeat("क्षि", 3));

        var parts = TextChunker.Split(text, 3);

        string.Concat(parts).Should().Be(text);
        parts.Should().OnlyContain(p => !char.IsHighSurrogate(p[p.Length - 1]) && !char.IsLowSurrogate(p[0]));
        parts.Should().OnlyContain(p => p.Length <= 4);
    }
}
