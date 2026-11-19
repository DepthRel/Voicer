using System.Text;

namespace Voicer.Core.Transcripts;

/// <summary>
/// Keeps a speech model from filling a text with one sentence: a piece that says word for word what one of the last few
/// pieces said is a loop of the model, not speech, and is left out. Short pieces ("Yes.", "Thank you.") may repeat.
/// </summary>
/// <remarks>
/// Whisper falls into such loops on silence, music or noise: a sentence it heard once comes back for every window that
/// follows. The engine stops most of them by not giving a window the text of the previous ones; this guard catches the
/// rest.
/// </remarks>
public sealed class RepetitionGuard
{
    /// <summary>
    /// How many words a piece needs before its repetition counts as a loop.
    /// </summary>
    private const int MIN_WORDS = 3;

    /// <summary>
    /// How many of the last kept pieces a new one is compared with.
    /// </summary>
    private const int MEMORY = 3;

    /// <summary>
    /// The words of the last kept pieces, the newest last.
    /// </summary>
    private readonly Queue<string> _recent = new();

    /// <summary>
    /// Tells whether a piece of speech is kept, and remembers it when it is.
    /// </summary>
    /// <param name="segment">The piece of speech, in the order the model gave it.</param>
    /// <returns><see langword="false"/> when the piece repeats one of the last pieces word for word.</returns>
    public bool Accept(TranscriptSegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var words = Words(segment.Text, out var count);
        if (count >= MIN_WORDS && _recent.Contains(words, StringComparer.Ordinal))
        {
            return false;
        }

        if (count > 0)
        {
            _recent.Enqueue(words);
            if (_recent.Count > MEMORY)
            {
                _recent.Dequeue();
            }
        }

        return true;
    }

    /// <summary>
    /// Takes the words of a text, in lower case, without punctuation, one space apart.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="count">How many words it has.</param>
    /// <returns>The words.</returns>
    private static string Words(string text, out int count)
    {
        var words = new StringBuilder();
        count = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord && words.Length > 0)
                {
                    words.Append(' ');
                }

                if (!inWord)
                {
                    count++;
                }

                words.Append(char.ToLowerInvariant(character));
                inWord = true;
            }
            else
            {
                inWord = false;
            }
        }

        return words.ToString();
    }
}
