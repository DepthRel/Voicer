using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;

namespace Voicer.UI.Controls;

/// <summary>
/// Shows its content when <see cref="IsExpanded"/> is set and hides it when it is cleared, sliding its height open and
/// shut and fading it in and out. A hidden content takes no place and cannot be reached with the keyboard.
/// </summary>
public sealed class Reveal : Decorator
{
    /// <summary>Defines the <see cref="IsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<Reveal, bool>(nameof(IsExpanded));

    /// <summary>Defines the <see cref="Openness"/> property.</summary>
    public static readonly StyledProperty<double> OpennessProperty =
        AvaloniaProperty.Register<Reveal, double>(nameof(Openness));

    /// <summary>How long the content takes to open or to close.</summary>
    private static readonly TimeSpan _duration = TimeSpan.FromMilliseconds(260);

    /// <summary>
    /// Initializes static members of the <see cref="Reveal"/> class.
    /// </summary>
    static Reveal()
    {
        AffectsMeasure<Reveal>(OpennessProperty);
        ClipToBoundsProperty.OverrideDefaultValue<Reveal>(true);
    }

    /// <summary>Gets or sets a value indicating whether the content is shown.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Gets or sets how far the content is open, from 0 (shut) to 1 (open); it follows <see cref="IsExpanded"/>.</summary>
    public double Openness
    {
        get => GetValue(OpennessProperty);
        set => SetValue(OpennessProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // The state it is shown in comes at once; only the changes after it are animated.
        Transitions = null;
        Openness = IsExpanded ? 1 : 0;
        Transitions =
        [
            new DoubleTransition { Property = OpennessProperty, Duration = _duration, Easing = new CubicEaseOut() },
        ];
        UpdateChild();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty)
        {
            Openness = IsExpanded ? 1 : 0;
            UpdateChild();
        }
        else if (change.Property == OpennessProperty || change.Property == ChildProperty)
        {
            UpdateChild();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { IsVisible: true } child)
        {
            return default;
        }

        child.Measure(availableSize.WithHeight(double.PositiveInfinity));

        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * Openness);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // The content keeps its whole height and is cut by the bounds while it slides.
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, Child.DesiredSize.Height)));

        return finalSize;
    }

    /// <summary>
    /// Fades the content with the openness and takes it away once it is shut.
    /// </summary>
    private void UpdateChild()
    {
        if (Child is not { } child)
        {
            return;
        }

        child.Opacity = Openness;
        child.IsVisible = IsExpanded || Openness > 0;
    }
}
