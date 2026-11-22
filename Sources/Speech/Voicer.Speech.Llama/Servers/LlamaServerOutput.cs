using System.Text.RegularExpressions;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// Reads what llama-server writes while it starts.
/// </summary>
internal static partial class LlamaServerOutput
{
    /// <summary>
    /// Reads the address from the line of llama-server that says it listens.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The address of the server, without a path, or <see langword="null"/> when the line does not say it.</returns>
    public static string? ReadAddress(string line)
    {
        var match = ListeningLine().Match(line);

        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// The line of llama-server that says where it listens: only an address of this computer is taken.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex(@"listening on (http://127\.0\.0\.1:\d{1,5})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ListeningLine();
}
