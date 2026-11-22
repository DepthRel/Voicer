using Voicer.Core.Search;

namespace Voicer.Core.Transcripts;

/// <summary>
/// Finds where the phrases of a record lie in its text (<see cref="Transcript.Text"/>) and which phrase is being said at
/// a moment of its sound.
/// </summary>
public static class TranscriptLayout
{
    /// <summary>
    /// Finds the place of every phrase in the text of its record.
    /// </summary>
    /// <param name="transcript">The record.</param>
    /// <returns>
    /// The place of every phrase, at the index of the phrase; <see langword="null"/> for a phrase without text, which the
    /// text leaves out.
    /// </returns>
    public static IReadOnlyList<TextMark?> Ranges(Transcript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var separator = Environment.NewLine.Length;
        var ranges = new List<TextMark?>(transcript.Segments.Count);
        var offset = 0;

        foreach (var segment in transcript.Segments)
        {
            var length = segment.Text.Trim().Length;
            if (length == 0)
            {
                ranges.Add(null);

                continue;
            }

            ranges.Add(new TextMark(offset, length));
            offset += length + separator;
        }

        return ranges;
    }

    /// <summary>
    /// Finds the phrase being said: the last phrase with text that has begun by the moment, so a pause keeps the phrase
    /// before it.
    /// </summary>
    /// <param name="transcript">The record.</param>
    /// <param name="position">The moment of the sound.</param>
    /// <returns>The index of the phrase, or <see langword="null"/> before the first phrase and in a record without text.</returns>
    public static int? SegmentAt(Transcript transcript, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        int? found = null;
        for (var i = 0; i < transcript.Segments.Count; i++)
        {
            var segment = transcript.Segments[i];
            if (segment.Start > position)
            {
                break;
            }

            if (segment.Text.Trim().Length > 0)
            {
                found = i;
            }
        }

        return found;
    }
}
