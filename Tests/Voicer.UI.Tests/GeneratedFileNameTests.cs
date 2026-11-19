using FluentAssertions;
using Voicer.UI.Services.Generation;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the names of the files of generated speech.
/// </summary>
public sealed class GeneratedFileNameTests
{
    /// <summary>
    /// The name takes the first words without what a file system refuses.
    /// </summary>
    [Fact]
    public void From_TakesTheFirstWords_WithoutUnsafeCharacters()
    {
        GeneratedFileName.From("Step 1: open \"Settings\" / then <click> the button and wait.").Should().Be("Step 1 open Settings then click.wav");
    }

    /// <summary>
    /// A text without words gets a plain name; a name Windows keeps for a device is not used alone.
    /// </summary>
    [Fact]
    public void From_KeepsTheNameUsable()
    {
        GeneratedFileName.From("?!.").Should().Be("speech.wav");
        GeneratedFileName.From("Con").Should().Be("speech Con.wav");
        GeneratedFileName.From(new string('a', 200)).Should().HaveLength(60 + GeneratedFileName.EXTENSION.Length);
    }
}
