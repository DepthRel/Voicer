using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Voicer.Core.Search;

namespace Voicer.UI.Controls;

/// <summary>
/// Shows a <see cref="MarkedText"/> in a <see cref="TextBlock"/> (or a <see cref="SelectableTextBlock"/>): the found
/// words bold in the accent colour on a soft accent ground, the found passage on the same ground, the phrase being said
/// on a stronger one in the primary text colour, the corrected places underlined in the colour of success. Set
/// <c>controls:TextMarks.Source</c> instead of the text of the block.
/// </summary>
public static class TextMarks
{
    /// <summary>
    /// The key of the brush of the ground of the marks.
    /// </summary>
    private const string GROUND_BRUSH = "AccentSubtleBrush";

    /// <summary>
    /// The key of the brush of the ground of the phrase being said, stronger than the other marks.
    /// </summary>
    private const string PHRASE_BRUSH = "AccentStrongBrush";

    /// <summary>
    /// The key of the brush of the text of the phrase being said.
    /// </summary>
    private const string PHRASE_TEXT_BRUSH = "TextPrimaryBrush";

    /// <summary>
    /// The key of the brush of the found words.
    /// </summary>
    private const string WORD_BRUSH = "AccentHoverBrush";

    /// <summary>
    /// The key of the brush of the corrected places.
    /// </summary>
    private const string CORRECTION_BRUSH = "SuccessBrush";

    /// <summary>
    /// Defines the <c>Source</c> attached property: the text with its marks.
    /// </summary>
    public static readonly AttachedProperty<MarkedText?> SourceProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, MarkedText?>("Source", typeof(TextMarks));

    /// <summary>
    /// Initializes static members of the <see cref="TextMarks"/> class.
    /// </summary>
    static TextMarks()
    {
        SourceProperty.Changed.AddClassHandler<TextBlock>((block, _) => Show(block));
    }

    /// <summary>
    /// Gets the text with its marks a block shows.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    public static MarkedText? GetSource(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return block.GetValue(SourceProperty);
    }

    /// <summary>
    /// Sets the text with its marks a block shows.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="value">The text, or <see langword="null"/> for none.</param>
    public static void SetSource(TextBlock block, MarkedText? value)
    {
        ArgumentNullException.ThrowIfNull(block);

        block.SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Finds where the marked passage of a block lies in it, once the block has laid its text out.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The area of the passage in the block, or <see langword="null"/> when it has none or is not laid out.</returns>
    public static Rect? PassageBounds(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return GetSource(block)?.Passage is { } passage ? RangeBounds(block, passage) : null;
    }

    /// <summary>
    /// Finds where a piece of the text of a block lies in it, once the block has laid its text out.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="range">The piece of the text.</param>
    /// <returns>The area of the piece in the block, or <see langword="null"/> when it is not laid out.</returns>
    public static Rect? RangeBounds(TextBlock block, TextMark range)
    {
        ArgumentNullException.ThrowIfNull(block);

        var rectangles = block.TextLayout.HitTestTextRange(range.Start, range.Length).ToList();
        if (rectangles.Count == 0)
        {
            return null;
        }

        var area = rectangles.Aggregate((a, b) => a.Union(b));

        return area.Translate(new Vector(block.Padding.Left, block.Padding.Top));
    }

    /// <summary>
    /// Finds the character of the text of a block under a point.
    /// </summary>
    /// <param name="block">The block.</param>
    /// <param name="point">The point, in the coordinates of the block.</param>
    /// <returns>The index of the character, or <see langword="null"/> when the point is not on the text.</returns>
    public static int? IndexAt(TextBlock block, Point point)
    {
        ArgumentNullException.ThrowIfNull(block);

        var at = new Point(point.X - block.Padding.Left, point.Y - block.Padding.Top);
        var index = block.TextLayout.HitTestPoint(at).CharacterHit.FirstCharacterIndex;

        // The hit says it is outside the text on every line after the first one, so the area of the character found
        // decides instead.
        var onCharacter = block.TextLayout.HitTestTextRange(index, 1).Any(area => area.Contains(at));

        return onCharacter ? index : null;
    }

    /// <summary>
    /// Puts the text of the block together from pieces: plain ones, and the marked ones with their look.
    /// </summary>
    /// <param name="block">The block.</param>
    private static void Show(TextBlock block)
    {
        var source = GetSource(block);
        if (source is null || !source.HasMarks)
        {
            block.Inlines = null;
            block.Text = source?.Text;

            return;
        }

        var ground = Brush(GROUND_BRUSH);
        var phraseGround = Brush(PHRASE_BRUSH);
        var phraseText = Brush(PHRASE_TEXT_BRUSH);
        var accent = Brush(WORD_BRUSH);
        var corrected = Brush(CORRECTION_BRUSH);
        var corrections = source.Corrections ?? [];
        var inlines = new InlineCollection();
        var cuts = Cuts(source);

        for (var i = 0; i + 1 < cuts.Count; i++)
        {
            var (from, to) = (cuts[i], cuts[i + 1]);
            var run = new Run(source.Text[from..to]);
            var inPassage = Covers(source.Passage, from, to);
            var inPhrase = Covers(source.Phrase, from, to);
            var inWord = source.Words.Any(word => Covers(word, from, to));
            var inCorrection = corrections.Any(mark => Covers(mark, from, to));

            if (inPhrase)
            {
                run.Background = phraseGround;
                run.Foreground = phraseText;
            }
            else if (inPassage || inWord)
            {
                run.Background = ground;
            }

            if (inWord)
            {
                run.FontWeight = FontWeight.SemiBold;
                run.Foreground = accent;
            }

            if (inCorrection)
            {
                run.Foreground = corrected;
                run.TextDecorations = TextDecorations.Underline;
            }

            inlines.Add(run);
        }

        block.Text = null;
        block.Inlines = inlines;
    }

    /// <summary>
    /// Tells whether a mark covers a piece of the text.
    /// </summary>
    /// <param name="mark">The mark, or <see langword="null"/>.</param>
    /// <param name="from">Where the piece starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns><see langword="true"/> when the piece lies inside the mark.</returns>
    private static bool Covers(TextMark? mark, int from, int to)
    {
        return mark is { } covering && from >= covering.Start && to <= covering.End;
    }

    /// <summary>
    /// Lists the places where a mark starts or ends, with the beginning and the end of the text.
    /// </summary>
    /// <param name="source">The text with its marks.</param>
    /// <returns>The places, in order, without repeats.</returns>
    private static List<int> Cuts(MarkedText source)
    {
        var marks = source.Words.Concat(source.Corrections ?? []);
        foreach (var single in new[] { source.Passage, source.Phrase })
        {
            if (single is { } mark)
            {
                marks = marks.Append(mark);
            }
        }

        var cuts = new SortedSet<int> { 0, source.Text.Length };
        foreach (var mark in marks)
        {
            cuts.Add(Math.Clamp(mark.Start, 0, source.Text.Length));
            cuts.Add(Math.Clamp(mark.End, 0, source.Text.Length));
        }

        return [.. cuts];
    }

    /// <summary>
    /// Takes a brush of the theme of the application.
    /// </summary>
    /// <param name="key">The key of the brush.</param>
    /// <returns>The brush, or <see langword="null"/> when the theme has none.</returns>
    private static IBrush? Brush(string key)
    {
        return Application.Current is { } application && application.TryFindResource(key, out var value) ? value as IBrush : null;
    }
}
