using System.Globalization;
using FluentAssertions;
using Voicer.Localization;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Localization;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the choice of the interface language: the system decides on the first start, the choice is remembered.
/// </summary>
public sealed class LanguageServiceTests : IDisposable
{
    /// <summary>
    /// A temporary settings file.
    /// </summary>
    private readonly string _file = Path.Combine(Path.GetTempPath(), "voicer-settings-" + Guid.NewGuid().ToString("N") + ".json");

    /// <summary>
    /// Removes the temporary file.
    /// </summary>
    public void Dispose()
    {
        File.Delete(_file);
    }

    /// <summary>
    /// The first start takes the language of the system and stores it.
    /// </summary>
    [Fact]
    public void Initialize_TakesTheSystemLanguageOnce()
    {
        var localizer = new Localizer(LanguageCatalog.LoadEmbedded());
        var store = new JsonSettingsStore(_file);

        new LanguageService(localizer, store, () => CultureInfo.GetCultureInfo("ru-RU")).Initialize();

        localizer.Current.Code.Should().Be("ru");
        store.Get(LanguageService.SETTING).Should().Be("ru");
    }

    /// <summary>
    /// A system language without texts gives English.
    /// </summary>
    [Fact]
    public void Initialize_FallsBackToEnglish()
    {
        var localizer = new Localizer(LanguageCatalog.LoadEmbedded(), "ru");

        new LanguageService(localizer, new JsonSettingsStore(_file), () => CultureInfo.GetCultureInfo("ja-JP")).Initialize();

        localizer.Current.Code.Should().Be("en");
    }

    /// <summary>
    /// A stored choice wins over the language of the system on the next start.
    /// </summary>
    [Fact]
    public void Initialize_KeepsTheStoredChoice()
    {
        var store = new JsonSettingsStore(_file);
        new LanguageService(new Localizer(LanguageCatalog.LoadEmbedded()), store, () => CultureInfo.GetCultureInfo("en-US")).Select("ru");

        var localizer = new Localizer(LanguageCatalog.LoadEmbedded());
        new LanguageService(localizer, store, () => CultureInfo.GetCultureInfo("en-US")).Initialize();

        localizer.Current.Code.Should().Be("ru");
    }
}
