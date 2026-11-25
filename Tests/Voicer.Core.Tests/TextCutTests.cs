using FluentAssertions;
using Voicer.Core.Text;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the cutting of a text to a length without breaking a character.
/// </summary>
public sealed class TextCutTests
{
    /// <summary>
    /// A text that fits stays as it is; a longer one is cut to the length.
    /// </summary>
    [Fact]
    public void Beginning_CutsToTheLength()
    {
        TextCut.Beginning("short", 10).Should().Be("short");
        TextCut.Beginning("longer text", 6).Should().Be("longer");
        TextCut.Beginning("text", 0).Should().BeEmpty();
    }

    /// <summary>
    /// A cut that would split a pair of surrogates leaves the whole pair out.
    /// </summary>
    [Fact]
    public void Beginning_NeverSplitsAPair()
    {
        TextCut.Beginning("ab😀c", 3).Should().Be("ab");
        TextCut.Beginning("ab😀c", 4).Should().Be("ab😀");
    }
}
