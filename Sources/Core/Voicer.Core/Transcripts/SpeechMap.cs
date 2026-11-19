namespace Voicer.Core.Transcripts;

/// <summary>
/// Joins the stretches of speech of a sound into a shorter sound, a short pause between them, and turns a moment of the
/// joined sound back into the moment of the original one. The speech model hears only speech, and the text keeps the
/// times of the file.
/// </summary>
public sealed class SpeechMap
{
    /// <summary>
    /// The stretches of speech, in the order of the sound, each with its place in both sounds; counted in samples.
    /// </summary>
    private readonly List<Piece> _pieces;

    /// <summary>
    /// The sample rate both sounds are counted in.
    /// </summary>
    private readonly int _sampleRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechMap"/> class.
    /// </summary>
    /// <param name="pieces">The stretches of speech, in order.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <param name="compactLength">The length of the joined sound, in samples.</param>
    private SpeechMap(List<Piece> pieces, int sampleRate, long compactLength)
    {
        _pieces = pieces;
        _sampleRate = sampleRate;
        CompactLength = compactLength;
    }

    /// <summary>
    /// Gets a value indicating whether the sound has no speech at all.
    /// </summary>
    public bool IsEmpty => _pieces.Count == 0;

    /// <summary>
    /// Gets the length of the joined sound, in samples.
    /// </summary>
    public long CompactLength { get; }

    /// <summary>
    /// Makes the map of a sound.
    /// </summary>
    /// <param name="speech">The stretches of speech the detector found, in any order; overlapping ones are merged.</param>
    /// <param name="sampleCount">How many samples the sound has.</param>
    /// <param name="sampleRate">The sample rate of the sound.</param>
    /// <param name="gap">The pause put between two stretches in the joined sound.</param>
    /// <returns>The map.</returns>
    public static SpeechMap Create(IReadOnlyList<SpeechSpan> speech, long sampleCount, int sampleRate, TimeSpan gap)
    {
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfLessThan(gap, TimeSpan.Zero);

        var gapSamples = ToSamples(gap, sampleRate);
        var stretches = speech
            .Select(s => (Start: Math.Clamp(ToSamples(s.Start, sampleRate), 0, sampleCount), End: Math.Clamp(ToSamples(s.End, sampleRate), 0, sampleCount)))
            .Where(s => s.End > s.Start)
            .OrderBy(s => s.Start)
            .ToList();

        var pieces = new List<Piece>();
        var compact = 0L;
        foreach (var (start, end) in stretches)
        {
            if (pieces.Count > 0 && start <= pieces[^1].OriginalEnd)
            {
                // Overlaps the previous stretch: one longer stretch.
                var last = pieces[^1];
                var grown = Math.Max(last.OriginalEnd, end) - last.OriginalStart;
                compact += grown - last.Length;
                pieces[^1] = last with { Length = grown };
                continue;
            }

            if (pieces.Count > 0)
            {
                compact += gapSamples;
            }

            pieces.Add(new Piece(start, compact, end - start));
            compact += end - start;
        }

        return new SpeechMap(pieces, sampleRate, compact);
    }

    /// <summary>
    /// Makes the joined sound: the stretches of speech one after another, silence between them.
    /// </summary>
    /// <param name="samples">The samples of the original sound.</param>
    /// <returns>The samples of the joined sound.</returns>
    public float[] Compact(ReadOnlySpan<float> samples)
    {
        var joined = new float[CompactLength];
        foreach (var piece in _pieces)
        {
            samples.Slice((int)piece.OriginalStart, (int)piece.Length).CopyTo(joined.AsSpan((int)piece.CompactStart, (int)piece.Length));
        }

        return joined;
    }

    /// <summary>
    /// Turns a moment of the joined sound into the moment of the original sound; a moment in a pause between two
    /// stretches is the end of the stretch before it.
    /// </summary>
    /// <param name="compact">The moment in the joined sound.</param>
    /// <returns>The moment in the original sound.</returns>
    public TimeSpan ToOriginal(TimeSpan compact)
    {
        if (_pieces.Count == 0)
        {
            return compact;
        }

        var position = ToSamples(compact, _sampleRate);
        var piece = _pieces[0];
        foreach (var candidate in _pieces)
        {
            if (candidate.CompactStart > position)
            {
                break;
            }

            piece = candidate;
        }

        var offset = Math.Clamp(position - piece.CompactStart, 0, piece.Length);

        return TimeSpan.FromSeconds((double)(piece.OriginalStart + offset) / _sampleRate);
    }

    /// <summary>
    /// Counts a moment in samples.
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <returns>The sample at the moment.</returns>
    private static long ToSamples(TimeSpan moment, int sampleRate)
    {
        return (long)Math.Round(moment.TotalSeconds * sampleRate);
    }

    /// <summary>
    /// A stretch of speech with its place in both sounds, in samples.
    /// </summary>
    /// <param name="OriginalStart">Where it begins in the original sound.</param>
    /// <param name="CompactStart">Where it begins in the joined sound.</param>
    /// <param name="Length">How long it is.</param>
    private sealed record Piece(long OriginalStart, long CompactStart, long Length)
    {
        /// <summary>
        /// Gets where it ends in the original sound.
        /// </summary>
        public long OriginalEnd => OriginalStart + Length;
    }
}
