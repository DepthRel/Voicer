using FluentAssertions;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the Markdown export of a transcript.
/// </summary>
public sealed class MarkdownTranscriptWriterTests
{
    /// <summary>
    /// A transcript of two pieces of speech and an empty one.
    /// </summary>
    private static readonly Transcript _transcript = new(
        Path.Combine("folder", "interview_1.mp4"),
        "Whisper Large v3",
        "NVIDIA GeForce RTX",
        "ru",
        new DateTimeOffset(2026, 11, 18, 10, 30, 0, TimeSpan.Zero),
        [
            new TranscriptSegment(TimeSpan.Zero, TimeSpan.FromSeconds(2), " Hello there. "),
            new TranscriptSegment(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3), "   "),
            new TranscriptSegment(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), "# not a heading"),
        ]);

    /// <summary>
    /// The document starts with the file name, describes how the text was made in the language of the reader, and
    /// gives a paragraph per piece of speech.
    /// </summary>
    [Fact]
    public void Write_BuildsTheDocument()
    {
        var markdown = new MarkdownTranscriptWriter(TestTexts.Russian).Write(_transcript);

        markdown.Should().StartWith("# interview\\_1.mp4\n\n");
        markdown.Should().Contain("- **" + TestTexts.Russian.Get("Export.Model") + ":** Whisper Large v3\n");
        markdown.Should().Contain("- **" + TestTexts.Russian.Get("Export.Language") + ":** " + TestTexts.Russian.Get("SpeechLanguage.ru") + "\n");
        markdown.Should().Contain("\nHello there.\n");
        markdown.Should().Contain("\n\\# not a heading\n");
    }

    /// <summary>
    /// A summary comes before the text, as the model wrote it in Markdown, each under its heading; without one there are
    /// no headings.
    /// </summary>
    [Fact]
    public void Write_PutsTheSummaryBeforeTheText()
    {
        var writer = new MarkdownTranscriptWriter(TestTexts.Russian);

        var markdown = writer.Write(_transcript, "- **Главное:** договорились\r\n- срок — пятница");

        var summary = "\n## " + TestTexts.Russian.Get("Export.Summary") + "\n\n- **Главное:** договорились\n- срок — пятница\n";
        var text = "\n## " + TestTexts.Russian.Get("Export.Text") + "\n";
        markdown.Should().Contain(summary).And.Contain(text);
        markdown.IndexOf(summary, StringComparison.Ordinal).Should().BeLessThan(markdown.IndexOf("Hello there.", StringComparison.Ordinal));
        writer.Write(_transcript).Should().NotContain("## ");
    }

    /// <summary>
    /// The text is the pieces of speech without empty ones, a line each.
    /// </summary>
    [Fact]
    public void Text_JoinsThePieces()
    {
        _transcript.Text.Should().Be("Hello there." + Environment.NewLine + "# not a heading");
    }

    /// <summary>
    /// Markup in the recognized text is shown as it is.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The escaped text.</param>
    [Theory]
    [InlineData("plain text", "plain text")]
    [InlineData("a *bold* claim", "a \\*bold\\* claim")]
    [InlineData("- item", "\\- item")]
    [InlineData("1. first", "1\\. first")]
    [InlineData("[link](x)", "\\[link\\](x)")]
    public void Escape_KeepsTheTextAsItIs(string text, string expected)
    {
        MarkdownTranscriptWriter.Escape(text).Should().Be(expected);
    }
}
