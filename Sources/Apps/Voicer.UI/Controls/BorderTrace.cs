using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Voicer.UI.Controls;

/// <summary>
/// A thin line of the colours of the logo that runs along the rounded border of its area, its tail fading behind it:
/// a sign that work goes on. It moves at an even speed along the whole border, corners included.
/// </summary>
/// <remarks>
/// The tail is drawn as layers of the same dash, each one shorter than the one before and all ending at the head, so the
/// light adds up towards the head; the layers go from violet at the end of the tail to blue at the head.
/// </remarks>
public sealed class BorderTrace : Control
{
    /// <summary>Defines the <see cref="Progress"/> property.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<BorderTrace, double>(nameof(Progress));

    /// <summary>Defines the <see cref="CornerRadius"/> property.</summary>
    public static readonly StyledProperty<double> CornerRadiusProperty =
        AvaloniaProperty.Register<BorderTrace, double>(nameof(CornerRadius), 10);

    /// <summary>Defines the <see cref="Thickness"/> property.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<BorderTrace, double>(nameof(Thickness), 1.5);

    /// <summary>Defines the <see cref="TailLength"/> property.</summary>
    public static readonly StyledProperty<double> TailLengthProperty =
        AvaloniaProperty.Register<BorderTrace, double>(nameof(TailLength), 180);

    /// <summary>How many layers make the fading tail.</summary>
    private const int LAYERS = 14;

    /// <summary>How much light each layer adds, from 0 to 1.</summary>
    private const double LAYER_OPACITY = 0.2;

    /// <summary>How much wider the soft glow under the line is than the line.</summary>
    private const double GLOW_WIDTH = 4;

    /// <summary>How much light each layer of the glow adds, from 0 to 1.</summary>
    private const double GLOW_OPACITY = 0.05;

    /// <summary>The colour of the end of the tail: the violet of the logo.</summary>
    private static readonly Color _tailColor = Color.Parse("#C084FC");

    /// <summary>The colour of the head: the blue of the logo.</summary>
    private static readonly Color _headColor = Color.Parse("#60A5FA");

    /// <summary>The border the line runs along, made for the current size, or <see langword="null"/>.</summary>
    private StreamGeometry? _path;

    /// <summary>The length of <see cref="_path"/>.</summary>
    private double _perimeter;

    /// <summary>The size <see cref="_path"/> was made for.</summary>
    private Size _pathSize;

    /// <summary>
    /// Initializes static members of the <see cref="BorderTrace"/> class.
    /// </summary>
    static BorderTrace()
    {
        AffectsRender<BorderTrace>(ProgressProperty, CornerRadiusProperty, ThicknessProperty, TailLengthProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<BorderTrace>(false);
    }

    /// <summary>Gets or sets where the head is on the border, from 0 to 1, once round from the top left corner.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Gets or sets the radius of the corners of the border.</summary>
    public double CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>Gets or sets the thickness of the line.</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>Gets or sets the length of the line with its tail.</summary>
    public double TailLength
    {
        get => GetValue(TailLengthProperty);
        set => SetValue(TailLengthProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The shape of the border follows its corners and the thickness of the line.
        if (change.Property == CornerRadiusProperty || change.Property == ThicknessProperty)
        {
            _path = null;
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= Thickness * 2 || Bounds.Height <= Thickness * 2)
        {
            return;
        }

        if (_path is null || _pathSize != Bounds.Size)
        {
            BuildPath(Bounds.Size);
        }

        var head = (Progress - Math.Floor(Progress)) * _perimeter;
        var tail = Math.Min(TailLength, _perimeter);
        for (var layer = LAYERS; layer >= 1; layer--)
        {
            // The longest layer first: violet; the shortest, at the head, last: blue.
            var length = tail * layer / LAYERS;
            var color = Mix(_headColor, _tailColor, (layer - 1) / (double)(LAYERS - 1));
            DrawDash(context, head, length, Thickness * GLOW_WIDTH, color, GLOW_OPACITY);
            DrawDash(context, head, length, Thickness, color, LAYER_OPACITY);
        }
    }

    /// <summary>
    /// Draws a piece of the border that ends at the head.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="head">Where the piece ends, along the border.</param>
    /// <param name="length">How long the piece is.</param>
    /// <param name="thickness">How thick it is drawn.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="opacity">Its opacity.</param>
    private void DrawDash(DrawingContext context, double head, double length, double thickness, Color color, double opacity)
    {
        // A dash pattern as long as the border, shifted so its one dash ends at the head; lengths count in thicknesses.
        var dashes = new[] { length / thickness, (_perimeter - length) / thickness };
        var offset = (length - head) / thickness;
        var brush = new SolidColorBrush(color, opacity);
        var pen = new Pen(brush, thickness, new DashStyle(dashes, offset), PenLineCap.Round);
        context.DrawGeometry(null, pen, _path!);
    }

    /// <summary>
    /// Makes the rounded border for a size, running clockwise from the top left corner, and measures it.
    /// </summary>
    /// <param name="size">The size of the area.</param>
    private void BuildPath(Size size)
    {
        var inset = Thickness / 2;
        var width = size.Width - Thickness;
        var height = size.Height - Thickness;
        var radius = Math.Max(0, Math.Min(CornerRadius - inset, Math.Min(width, height) / 2));
        var corner = new Size(radius, radius);

        var path = new StreamGeometry();
        using (var figure = path.Open())
        {
            figure.BeginFigure(new Point(inset + radius, inset), false);
            figure.LineTo(new Point(inset + width - radius, inset));
            figure.ArcTo(new Point(inset + width, inset + radius), corner, 0, false, SweepDirection.Clockwise);
            figure.LineTo(new Point(inset + width, inset + height - radius));
            figure.ArcTo(new Point(inset + width - radius, inset + height), corner, 0, false, SweepDirection.Clockwise);
            figure.LineTo(new Point(inset + radius, inset + height));
            figure.ArcTo(new Point(inset, inset + height - radius), corner, 0, false, SweepDirection.Clockwise);
            figure.LineTo(new Point(inset, inset + radius));
            figure.ArcTo(new Point(inset + radius, inset), corner, 0, false, SweepDirection.Clockwise);
            figure.EndFigure(true);
        }

        _path = path;
        _pathSize = size;
        _perimeter = (2 * (width + height)) - (8 * radius) + (2 * Math.PI * radius);
    }

    /// <summary>
    /// Mixes two colours.
    /// </summary>
    /// <param name="from">The colour at 0.</param>
    /// <param name="to">The colour at 1.</param>
    /// <param name="amount">How far towards <paramref name="to"/>, from 0 to 1.</param>
    /// <returns>The mixed colour.</returns>
    private static Color Mix(Color from, Color to, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
            (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
            (byte)Math.Round(from.B + ((to.B - from.B) * amount)));
    }
}
