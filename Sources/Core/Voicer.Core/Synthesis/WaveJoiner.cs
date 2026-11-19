using Voicer.Localization;

namespace Voicer.Core.Synthesis;

/// <summary>
/// Joins the pieces of a generated speech into one WAV file, with silence between them. The samples are copied as they
/// are, so nothing of the sound is lost; all pieces must be of one format, as a model gives them.
/// </summary>
public static class WaveJoiner
{
    /// <summary>
    /// The size of the buffer the samples are copied through.
    /// </summary>
    private const int BUFFER_SIZE = 81920;

    /// <summary>
    /// Joins the pieces.
    /// </summary>
    /// <param name="pieces">The WAV files of the pieces, in their order; at least one.</param>
    /// <param name="pausesAfter">The silence after every piece; the one after the last piece is not written.</param>
    /// <param name="destination">The WAV file to write; it is replaced.</param>
    /// <returns>Where every piece lies in the joined file.</returns>
    /// <exception cref="InvalidDataException">A piece is not a WAV file, or the pieces differ in format.</exception>
    public static IReadOnlyList<WaveSpan> Join(IReadOnlyList<string> pieces, IReadOnlyList<TimeSpan> pausesAfter, string destination)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(pausesAfter);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (pieces.Count == 0 || pausesAfter.Count != pieces.Count)
        {
            throw new ArgumentException("Every piece needs its pause, and there must be at least one piece.", nameof(pausesAfter));
        }

        var layouts = pieces.Select(Read).ToList();
        var format = layouts[0].Format;
        for (var i = 1; i < layouts.Count; i++)
        {
            if (layouts[i].Format != format)
            {
                throw LocalizedError.Create(m => new InvalidDataException(m), "Generation.WaveMismatch", Path.GetFileName(pieces[i]));
            }
        }

        var silences = pausesAfter.Select((pause, i) => i == pieces.Count - 1 ? 0 : format.BytesOf(pause)).ToList();
        var total = layouts.Sum(l => l.DataLength) + silences.Sum();

        using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        WaveFile.WriteHeader(output, format, total);

        var spans = new List<WaveSpan>(pieces.Count);
        var written = 0L;
        for (var i = 0; i < pieces.Count; i++)
        {
            var start = format.DurationOf(written);
            CopySamples(pieces[i], layouts[i], output);
            written += layouts[i].DataLength;
            spans.Add(new WaveSpan(start, format.DurationOf(written)));

            WriteSilence(output, silences[i]);
            written += silences[i];
        }

        return spans;
    }

    /// <summary>
    /// Reads the layout of a piece.
    /// </summary>
    /// <param name="piece">The WAV file of the piece.</param>
    /// <returns>Its format and the place of its samples.</returns>
    private static WaveLayout Read(string piece)
    {
        using var input = new FileStream(piece, FileMode.Open, FileAccess.Read, FileShare.Read);

        return WaveFile.ReadLayout(input, Path.GetFileName(piece));
    }

    /// <summary>
    /// Copies the samples of a piece to the output.
    /// </summary>
    /// <param name="piece">The WAV file of the piece.</param>
    /// <param name="layout">Where its samples are.</param>
    /// <param name="output">The joined file.</param>
    private static void CopySamples(string piece, WaveLayout layout, Stream output)
    {
        using var input = new FileStream(piece, FileMode.Open, FileAccess.Read, FileShare.Read);
        input.Seek(layout.DataOffset, SeekOrigin.Begin);

        var buffer = new byte[BUFFER_SIZE];
        var left = layout.DataLength;
        while (left > 0)
        {
            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
            if (read == 0)
            {
                throw LocalizedError.Create(m => new InvalidDataException(m), "Generation.WaveInvalid", Path.GetFileName(piece));
            }

            output.Write(buffer, 0, read);
            left -= read;
        }
    }

    /// <summary>
    /// Writes silence: zero samples are silence both for integers and for floating point.
    /// </summary>
    /// <param name="output">The joined file.</param>
    /// <param name="bytes">The bytes of silence.</param>
    private static void WriteSilence(Stream output, long bytes)
    {
        var zeros = new byte[(int)Math.Min(bytes, BUFFER_SIZE)];
        var left = bytes;
        while (left > 0)
        {
            var count = (int)Math.Min(zeros.Length, left);
            output.Write(zeros, 0, count);
            left -= count;
        }
    }
}
