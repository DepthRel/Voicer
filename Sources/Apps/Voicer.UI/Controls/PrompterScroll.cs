using Avalonia;

namespace Voicer.UI.Controls;

/// <summary>
/// Decides how the text of a record follows the phrase being said: the phrase that leaves the view is put a third from
/// the top, so what follows it can be read ahead; a phrase in view, or a text the user scrolled away from, is left alone.
/// </summary>
public static class PrompterScroll
{
    /// <summary>
    /// The part of the view kept above a place it is put at.
    /// </summary>
    private const double LEAD = 1.0 / 3;

    /// <summary>
    /// Finds where to scroll the text for a new phrase being said.
    /// </summary>
    /// <param name="phrase">The area of the new phrase in the text.</param>
    /// <param name="previous">The area of the phrase said before it, or <see langword="null"/> for the first one.</param>
    /// <param name="offset">How far the text is scrolled now.</param>
    /// <param name="viewport">The height of the view.</param>
    /// <param name="extent">The height of the whole text.</param>
    /// <returns>
    /// The new offset, or <see langword="null"/> to leave the text: the phrase is in view, or the phrase before it was not,
    /// which means the user scrolled away to read elsewhere.
    /// </returns>
    public static double? Follow(Rect phrase, Rect? previous, double offset, double viewport, double extent)
    {
        if (IsInView(phrase, offset, viewport))
        {
            return null;
        }

        if (previous is { } before && !IsInView(before, offset, viewport))
        {
            return null;
        }

        return Place(phrase, viewport, extent);
    }

    /// <summary>
    /// Finds the offset that puts a place a third from the top of the view, within the text.
    /// </summary>
    /// <param name="place">The area of the place in the text.</param>
    /// <param name="viewport">The height of the view.</param>
    /// <param name="extent">The height of the whole text.</param>
    /// <returns>The offset.</returns>
    public static double Place(Rect place, double viewport, double extent)
    {
        return Math.Clamp(place.Top - (viewport * LEAD), 0, Math.Max(0, extent - viewport));
    }

    /// <summary>
    /// Tells whether an area of the text is wholly in view.
    /// </summary>
    /// <param name="area">The area.</param>
    /// <param name="offset">How far the text is scrolled.</param>
    /// <param name="viewport">The height of the view.</param>
    /// <returns><see langword="true"/> when the area is in view.</returns>
    private static bool IsInView(Rect area, double offset, double viewport)
    {
        return area.Top >= offset && area.Bottom <= offset + viewport;
    }
}
