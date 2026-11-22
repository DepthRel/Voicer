using Avalonia;
using FluentAssertions;
using Voicer.UI.Controls;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of how the text follows the phrase being said: put a third from the top when it leaves the view, left alone
/// when it is in view or the user scrolled away to read elsewhere.
/// </summary>
public sealed class PrompterScrollTests
{
    /// <summary>
    /// The height of the view of the text in the tests.
    /// </summary>
    private const double VIEWPORT = 300;

    /// <summary>
    /// The height of the whole text in the tests.
    /// </summary>
    private const double EXTENT = 2000;

    /// <summary>
    /// A phrase in view leaves the text where it is.
    /// </summary>
    [Fact]
    public void Follow_PhraseInView_StaysPut()
    {
        PrompterScroll.Follow(Line(100), Line(80), 0, VIEWPORT, EXTENT).Should().BeNull();
    }

    /// <summary>
    /// A phrase that leaves the view at the bottom is put a third from the top, so what follows it is seen.
    /// </summary>
    [Fact]
    public void Follow_PhraseBelowTheView_PutsItAThirdFromTheTop()
    {
        PrompterScroll.Follow(Line(320), Line(280), 0, VIEWPORT, EXTENT).Should().Be(220);
    }

    /// <summary>
    /// The first phrase followed is shown wherever it is.
    /// </summary>
    [Fact]
    public void Follow_TheFirstPhrase_IsShown()
    {
        PrompterScroll.Follow(Line(500), null, 0, VIEWPORT, EXTENT).Should().Be(400);
    }

    /// <summary>
    /// When the user scrolled away from the phrase being said, the text stays where the user put it.
    /// </summary>
    [Fact]
    public void Follow_AfterTheUserScrolledAway_StaysPut()
    {
        PrompterScroll.Follow(Line(130), Line(100), 1000, VIEWPORT, EXTENT).Should().BeNull();
    }

    /// <summary>
    /// Near the end of the text the view stops at the end instead of leaving an empty space below it.
    /// </summary>
    [Fact]
    public void Follow_NearTheEnd_StopsAtTheEndOfTheText()
    {
        PrompterScroll.Follow(Line(1980), Line(1700), 1500, VIEWPORT, EXTENT).Should().Be(EXTENT - VIEWPORT);
    }

    /// <summary>
    /// A place is put a third from the top of the view, never above the beginning of the text.
    /// </summary>
    [Fact]
    public void Place_PutsAThirdFromTheTop()
    {
        PrompterScroll.Place(Line(400), VIEWPORT, EXTENT).Should().Be(300);
        PrompterScroll.Place(Line(50), VIEWPORT, EXTENT).Should().Be(0);
    }

    /// <summary>
    /// Builds the area of a line of text.
    /// </summary>
    /// <param name="top">Where the line starts.</param>
    /// <returns>The area of the line, 20 high.</returns>
    private static Rect Line(double top)
    {
        return new Rect(14, top, 400, 20);
    }
}
