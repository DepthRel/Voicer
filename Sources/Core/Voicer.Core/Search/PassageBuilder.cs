using System.Text;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Search;

/// <summary>
/// Puts the fragments of a recognized text together into passages of a few sentences: long enough to carry a thought
/// for the search by meaning, short enough to point at a moment of the record.
/// </summary>
public static class PassageBuilder
{
    /// <summary>
    /// How long a passage grows before the next fragment starts a new one, in characters.
    /// </summary>
    public const int TARGET_LENGTH = 600;

    /// <summary>
    /// Builds the passages of a text.
    /// </summary>
    /// <param name="segments">The fragments of the text, in their order.</param>
    /// <returns>The passages, in their order; fragments without text are left out.</returns>
    public static IReadOnlyList<Passage> Build(IReadOnlyList<TranscriptSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var passages = new List<Passage>();
        var text = new StringBuilder();
        var start = TimeSpan.Zero;
        var end = TimeSpan.Zero;

        foreach (var segment in segments)
        {
            var piece = segment.Text.Trim();
            if (piece.Length == 0)
            {
                continue;
            }

            if (text.Length == 0)
            {
                start = segment.Start;
            }
            else
            {
                text.Append(' ');
            }

            text.Append(piece);
            end = segment.End;

            if (text.Length >= TARGET_LENGTH)
            {
                passages.Add(new Passage(start, end, text.ToString()));
                text.Clear();
            }
        }

        if (text.Length > 0)
        {
            passages.Add(new Passage(start, end, text.ToString()));
        }

        return passages;
    }
}
