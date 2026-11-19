using System.Text;
using Voicer.Localization;

namespace Voicer.Core.Transcripts;

/// <summary>
/// Writes a transcript as a Markdown document: the name of the file as the title, how the text was made, then the text,
/// a paragraph per piece of speech.
/// </summary>
public sealed class MarkdownTranscriptWriter
{
    /// <summary>
    /// The extension of a Markdown file, without the dot.
    /// </summary>
    public const string EXTENSION = "md";

    /// <summary>
    /// The characters that change the meaning of Markdown inside a line.
    /// </summary>
    private const string INLINE_SPECIALS = "\\`*_[]<>|";

    /// <summary>
    /// The localizer of the labels of the document.
    /// </summary>
    private readonly ILocalizer _localizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownTranscriptWriter"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the labels of the document.</param>
    public MarkdownTranscriptWriter(ILocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    /// <summary>
    /// Writes a transcript as Markdown.
    /// </summary>
    /// <param name="transcript">The transcript.</param>
    /// <param name="summary">
    /// Its summary, already in Markdown as the language model writes it, or <see langword="null"/>; it comes before the
    /// text, each under a heading of its own.
    /// </param>
    /// <returns>The Markdown document, with <c>\n</c> line ends.</returns>
    public string Write(Transcript transcript, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var markdown = new StringBuilder();
        markdown.Append("# ").Append(Escape(transcript.SourceName)).Append("\n\n");

        AppendField(markdown, "Export.Model", transcript.ModelName);
        AppendField(markdown, "Export.Device", transcript.DeviceName);
        if (!string.IsNullOrWhiteSpace(transcript.Language))
        {
            AppendField(markdown, "Export.Language", SpeechLanguageNames.Name(_localizer, transcript.Language));
        }

        AppendField(markdown, "Export.Date", transcript.CreatedAt.ToString("g", _localizer.Culture));
        markdown.Append("\n---\n");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            markdown.Append("\n## ").Append(_localizer.Get("Export.Summary")).Append("\n\n");
            markdown.Append(summary.Trim().ReplaceLineEndings("\n")).Append('\n');
            markdown.Append("\n## ").Append(_localizer.Get("Export.Text")).Append('\n');
        }

        foreach (var segment in transcript.Segments)
        {
            var text = segment.Text.Trim();
            if (text.Length > 0)
            {
                markdown.Append('\n').Append(Escape(text)).Append('\n');
            }
        }

        return markdown.ToString();
    }

    /// <summary>
    /// Makes a text safe to put into a Markdown paragraph: it is shown as it is, never as markup.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var escaped = new StringBuilder(text.Length + 8);
        foreach (var character in text)
        {
            if (INLINE_SPECIALS.Contains(character))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        // A line that starts like a heading, a quote, a list or a rule would become one.
        var result = escaped.ToString();
        if (result.Length > 0 && "#>-+=".Contains(result[0]))
        {
            return "\\" + result;
        }

        var digits = result.TakeWhile(char.IsDigit).Count();
        if (digits > 0 && digits < result.Length && result[digits] is '.' or ')')
        {
            return result[..digits] + "\\" + result[digits..];
        }

        return result;
    }

    /// <summary>
    /// Adds a line of the description of the transcript: a label in bold and its value.
    /// </summary>
    /// <param name="markdown">The document.</param>
    /// <param name="labelKey">The key of the label.</param>
    /// <param name="value">The value.</param>
    private void AppendField(StringBuilder markdown, string labelKey, string value)
    {
        markdown.Append("- **").Append(Escape(_localizer.Get(labelKey))).Append(":** ").Append(Escape(value)).Append('\n');
    }
}
