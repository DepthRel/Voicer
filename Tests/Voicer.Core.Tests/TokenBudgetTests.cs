using FluentAssertions;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the estimate of the tokens of a text and of the fitting of texts into them.
/// </summary>
public sealed class TokenBudgetTests
{
    /// <summary>
    /// A script a tokenizer cuts finer costs more tokens per character than a script written in words.
    /// </summary>
    [Fact]
    public void Estimate_CountsDenseScriptsAsAWholeTokenPerSign()
    {
        TokenBudget.Estimate(new string('a', 100)).Should().Be(40);
        TokenBudget.Estimate(new string('я', 100)).Should().Be(40);
        TokenBudget.Estimate(new string('中', 100)).Should().Be(100);
        TokenBudget.Estimate(new string('ก', 100)).Should().Be(100);
        TokenBudget.Estimate(new string('한', 100)).Should().Be(100);
        TokenBudget.Estimate(string.Empty).Should().Be(0);
    }

    /// <summary>
    /// A Chinese text is cut into parts that fit the tokens, where the same number of Latin letters would fit at once.
    /// </summary>
    [Fact]
    public void Split_FitsEveryPartIntoTheTokens()
    {
        var chinese = string.Concat(Enumerable.Repeat("今天天气很好。", 300));

        var parts = TokenBudget.Split(chinese, 1000);

        parts.Should().HaveCountGreaterThan(1);
        parts.Should().OnlyContain(p => TokenBudget.Estimate(p) <= 1000);
        string.Concat(parts).Should().Be(chinese);
        TokenBudget.Split(new string('a', chinese.Length), 1000).Should().ContainSingle();
    }

    /// <summary>
    /// A text denser in one place than on average is cut again there, so no part overflows.
    /// </summary>
    [Fact]
    public void Split_CutsADensePlaceAgain()
    {
        var text = new string('a', 2000) + "\n" + new string('中', 500);

        var parts = TokenBudget.Split(text, 300);

        parts.Should().OnlyContain(p => TokenBudget.Estimate(p) <= 300);
    }

    /// <summary>
    /// The beginning that fits is cut between characters, never inside a pair of surrogates.
    /// </summary>
    [Fact]
    public void Cut_KeepsTheBeginningThatFits()
    {
        TokenBudget.Cut("short", 100).Should().Be("short");
        TokenBudget.Cut(new string('中', 10), 4).Should().Be(new string('中', 4));
        TokenBudget.Cut("𠀀𠀀𠀀", 2).Should().Be("𠀀𠀀");
    }
}
