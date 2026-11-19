using FluentAssertions;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the names of the languages of speech.
/// </summary>
public sealed class SpeechLanguageNamesTests
{
    /// <summary>
    /// A detected code becomes the full name in the language of the reader, whatever its case.
    /// </summary>
    [Fact]
    public void Name_GivesTheFullName()
    {
        SpeechLanguageNames.Name(TestTexts.Russian, "de").Should().Be(TestTexts.Russian.Get("SpeechLanguage.de"));
        SpeechLanguageNames.Name(TestTexts.English, "FR").Should().Be(TestTexts.English.Get("SpeechLanguage.fr"));
        SpeechLanguageNames.Name(TestTexts.English, "yue").Should().Be(TestTexts.English.Get("SpeechLanguage.yue"));
    }

    /// <summary>
    /// A code Whisper does not know is shown as it came, never as a key of the packs.
    /// </summary>
    [Fact]
    public void Name_KeepsAnUnknownCode()
    {
        SpeechLanguageNames.Name(TestTexts.Russian, "xx").Should().Be("xx");
    }

    /// <summary>
    /// The list has every code once.
    /// </summary>
    [Fact]
    public void Codes_AreUnique()
    {
        SpeechLanguageNames.Codes.Should().OnlyHaveUniqueItems().And.HaveCount(100);
    }
}
