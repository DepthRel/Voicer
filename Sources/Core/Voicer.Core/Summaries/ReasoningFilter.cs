namespace Voicer.Core.Summaries;

/// <summary>
/// Removes the reasoning that thinking models (Qwen3, DeepSeek-R1 and the like) write before the answer between
/// <c>&lt;think&gt;</c> and <c>&lt;/think&gt;</c>: the user reads only the answer.
/// </summary>
public static class ReasoningFilter
{
    /// <summary>
    /// The tag that opens the reasoning.
    /// </summary>
    private const string OPEN = "<think>";

    /// <summary>
    /// The tag that closes the reasoning.
    /// </summary>
    private const string CLOSE = "</think>";

    /// <summary>
    /// Removes the reasoning from an answer, whole or still arriving.
    /// </summary>
    /// <param name="answer">The answer of the model.</param>
    /// <returns>The answer without its reasoning, trimmed.</returns>
    public static string Strip(string answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        var text = answer;

        // Some servers drop the opening tag: everything up to a lone closing one is the reasoning.
        var close = text.IndexOf(CLOSE, StringComparison.OrdinalIgnoreCase);
        var open = text.IndexOf(OPEN, StringComparison.OrdinalIgnoreCase);
        if (close >= 0 && (open < 0 || open > close))
        {
            text = text[(close + CLOSE.Length)..];
        }

        while ((open = text.IndexOf(OPEN, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            close = text.IndexOf(CLOSE, open, StringComparison.OrdinalIgnoreCase);

            // An unclosed reasoning is still being written: nothing after it is the answer yet.
            text = close < 0 ? text[..open] : text[..open] + text[(close + CLOSE.Length)..];
        }

        return text.Trim();
    }
}
