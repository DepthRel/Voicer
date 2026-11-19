using FluentAssertions;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the removal of the reasoning of thinking models from their answers.
/// </summary>
public sealed class ReasoningFilterTests
{
    /// <summary>
    /// The reasoning between the tags goes, the answer stays.
    /// </summary>
    [Fact]
    public void Strip_RemovesTheReasoning()
    {
        ReasoningFilter.Strip("<think>Let me see…\nyes.</think>\n\nThe summary.").Should().Be("The summary.");
    }

    /// <summary>
    /// A reasoning still being written hides everything after it.
    /// </summary>
    [Fact]
    public void Strip_HidesAnUnfinishedReasoning()
    {
        ReasoningFilter.Strip("<think>Still thinking about").Should().BeEmpty();
    }

    /// <summary>
    /// A server that dropped the opening tag still has its reasoning removed.
    /// </summary>
    [Fact]
    public void Strip_RemovesAReasoningWithoutItsOpeningTag()
    {
        ReasoningFilter.Strip("thinking…</think>The summary.").Should().Be("The summary.");
    }

    /// <summary>
    /// An answer without reasoning stays as it is.
    /// </summary>
    [Fact]
    public void Strip_KeepsAPlainAnswer()
    {
        ReasoningFilter.Strip("  - point one\n- point two  ").Should().Be("- point one\n- point two");
    }
}
