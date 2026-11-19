using FluentAssertions;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the wording of sizes and durations.
/// </summary>
public sealed class DisplayTextTests
{
    /// <summary>
    /// Sizes take binary units in the language of the reader.
    /// </summary>
    [Fact]
    public void Bytes_UsesTheUnitsOfTheLanguage()
    {
        DisplayText.Bytes(TestTexts.English, 3_095_033_483).Should().Be("2.9 GB");
        DisplayText.Bytes(TestTexts.Russian, 3_095_033_483).Should().Be("2,9 ГБ");
        DisplayText.Bytes(TestTexts.English, 5 * 1024 * 1024).Should().Be("5 MB");
        DisplayText.Bytes(TestTexts.English, 12).Should().Be("12 B");
        DisplayText.Speed(TestTexts.English, 2048).Should().Be("2 KB/s");
    }

    /// <summary>
    /// Durations read like a clock.
    /// </summary>
    [Fact]
    public void Duration_ReadsLikeAClock()
    {
        DisplayText.Duration(TimeSpan.FromSeconds(65)).Should().Be("1:05");
        DisplayText.Duration(new TimeSpan(1, 2, 3)).Should().Be("1:02:03");
    }
}
