using FluentAssertions;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the settings file: what is kept, a damaged file, and a file that cannot be read for the moment.
/// </summary>
public sealed class JsonSettingsStoreTests : IDisposable
{
    /// <summary>
    /// A temporary data folder.
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "voicer-settings-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Gets the settings file of the test.
    /// </summary>
    private string FilePath => Path.Combine(_folder, "settings.json");

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>
    /// Every setting is kept beside the others.
    /// </summary>
    [Fact]
    public void Set_KeepsTheOtherSettings()
    {
        var store = new JsonSettingsStore(FilePath);

        store.Set("Language", "de");
        store.Set("Theme", "Dark");

        var reopened = new JsonSettingsStore(FilePath);
        reopened.Get("Language").Should().Be("de");
        reopened.Get("Theme").Should().Be("Dark");
    }

    /// <summary>
    /// A damaged file reads as empty and the next write replaces it.
    /// </summary>
    [Fact]
    public void DamagedFile_IsReplaced()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ not json");
        var store = new JsonSettingsStore(FilePath);

        store.Get("Language").Should().BeNull();
        store.Set("Language", "fr");

        store.Get("Language").Should().Be("fr");
    }

    /// <summary>
    /// A file another program holds is not written over with the one new setting: the others stay.
    /// </summary>
    [Fact]
    public void FileThatCannotBeRead_IsNotWrittenOver()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Set("Language", "de");
        store.Set("Theme", "Dark");

        using (new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Set("Theme", "Light");
        }

        store.Get("Language").Should().Be("de");
        store.Get("Theme").Should().Be("Dark");
    }
}
