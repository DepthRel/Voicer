using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Voicer.Core.Search;

namespace Voicer.UI.Controls;

/// <summary>
/// Shows a <see cref="MarkedText"/> in a <see cref="TextBlock"/> (or a <see cref="SelectableTextBlock"/>): the found
/// words bold in the accent colour on a soft accent ground, the found passage on the same ground. Set
/// <c>controls:TextMarks.Source</c> instead of the text of the block.
/// </summary>
public static class TextMarks
{
    /// <summary>
    /// The key of the brush of the ground of the marks.
    /// </summary>
    private const string GROUND_BRUSH = "AccentSubtleBrush";

    /// <summary>
    /// The key of the brush of the found words.
    /// </summary>
    private const string WORD_BRUSH = "AccentHoverBrush";

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

        if (GetSource(block)?.Passage is not { } passage)
        {
            return null;
        }

        var rectangles = block.TextLayout.HitTestTextRange(passage.Start, passage.Length).ToList();
        if (rectangles.Count == 0)
        {
            return null;
        }

        var area = rectangles.Aggregate((a, b) => a.Union(b));

        return area.Translate(new Vector(block.Padding.Left, block.Padding.Top));
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
        var accent = Brush(WORD_BRUSH);
        var inlines = new InlineCollection();
        var cuts = Cuts(source);

        for (var i = 0; i + 1 < cuts.Count; i++)
        {
            var (from, to) = (cuts[i], cuts[i + 1]);
            var run = new Run(source.Text[from..to]);
            var inPassage = source.Passage is { } passage && from >= passage.Start && to <= passage.End;
            var inWord = source.Words.Any(word => from >= word.Start && to <= word.End);

            if (inPassage || inWord)
            {
                run.Background = ground;
            }

            if (inWord)
            {
                run.FontWeight = FontWeight.SemiBold;
                run.Foreground = accent;
            }

            inlines.Add(run);
        }

        block.Text = null;
        block.Inlines = inlines;
    }

    /// <summary>
    /// Lists the places where a mark starts or ends, with the beginning and the end of the text.
    /// </summary>
    /// <param name="source">The text with its marks.</param>
    /// <returns>The places, in order, without repeats.</returns>
    private static List<int> Cuts(MarkedText source)
    {
        var cuts = new SortedSet<int> { 0, source.Text.Length };
        foreach (var mark in source.Passage is { } passage ? source.Words.Append(passage) : source.Words)
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
