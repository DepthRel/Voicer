using System.Text.RegularExpressions;
using FluentAssertions;
using Voicer.Core.Enums;
using Voicer.Core.Models;
using Voicer.Core.Synthesis;
using Voicer.Core.Transcripts;

namespace Voicer.Localization.Tests;

/// <summary>
/// Tests of the language packs: every language has the same texts with the same placeholders, and every key the code
/// and the views ask for exists.
/// </summary>
public sealed partial class LanguagePackTests
{
    /// <summary>
    /// The embedded languages.
    /// </summary>
    private static readonly LanguageCatalog _catalog = LanguageCatalog.LoadEmbedded();

    /// <summary>
    /// The packs are English, German, Spanish, French, Portuguese and Russian: English first, then the others by their
    /// own names.
    /// </summary>
    [Fact]
    public void Catalog_OffersEnglishThenTheOthersByName()
    {
        _catalog.Languages.Select(l => l.Code).Should().Equal("en", "de", "es", "fr", "pt", "ru");
    }

    /// <summary>
    /// Every pack has exactly the keys of the English pack.
    /// </summary>
    [Fact]
    public void Packs_HaveTheSameKeys()
    {
        var english = _catalog.Default.Keys.ToHashSet();
        foreach (var language in _catalog.Languages)
        {
            var keys = _catalog.Find(language.Code)!.Keys;

            keys.Should().BeEquivalentTo(english, $"the {language.Code} pack must have the keys of the English one");
        }
    }

    /// <summary>
    /// A text has the same placeholders in every language.
    /// </summary>
    [Fact]
    public void Packs_HaveTheSamePlaceholders()
    {
        var english = _catalog.Default;
        foreach (var language in _catalog.Languages)
        {
            var pack = _catalog.Find(language.Code)!;
            foreach (var key in english.Keys)
            {
                english.TryGet(key, out var expected);
                pack.TryGet(key, out var actual);

                Placeholders(actual).Should().BeEquivalentTo(Placeholders(expected), $"{language.Code}: {key}");
            }
        }
    }

    /// <summary>
    /// Every failure of the worker, every model of both catalogs, every quantization, every language of the voice models
    /// and of Whisper, the accents and kinds of the voices and every kind of record has its text.
    /// </summary>
    [Fact]
    public void KeysBuiltFromValues_Exist()
    {
        var keys = Enum.GetNames<WorkerErrorCode>().Select(code => "Worker.Error." + code)
            .Concat(SpeechModelCatalog.All.Select(m => m.DescriptionKey))
            .Concat(Enum.GetNames<ModelQuantization>().SelectMany(q => new[] { $"Quantization.{q}.Name", $"Quantization.{q}.Description" }))
            .Concat(VoiceModelCatalog.All.Select(m => m.DescriptionKey))
            .Concat(VoiceModelCatalog.All.SelectMany(m => m.Languages).Distinct().Select(code => "SpeechLanguage." + code))
            .Concat(SpeechLanguageNames.Codes.Select(code => SpeechLanguageNames.KEY_PREFIX + code))
            .Concat(["Voice.Accent.a", "Voice.Accent.b", "Voice.Kind.f", "Voice.Kind.m"])
            .Concat(Enum.GetNames<RecordKind>().Select(kind => "History.Kind." + kind))
            .Concat(["Device.RamTotal", "Device.VramTotal"]);

        keys.Where(k => !_catalog.Default.TryGet(k, out _)).Should().BeEmpty();
    }

    /// <summary>
    /// Every key named in the code (<c>Get("…")</c>, <c>Format("…")</c>, the errors) and in the views
    /// (<c>{loc:Loc …}</c>) is in the English pack.
    /// </summary>
    [Fact]
    public void KeysUsedInSources_Exist()
    {
        var prefixes = _catalog.Default.Keys.Select(k => k.Split('.')[0]).ToHashSet(StringComparer.Ordinal);
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in SourceFiles("*.cs"))
        {
            foreach (Match match in KeyLiteral().Matches(File.ReadAllText(file)))
            {
                var key = match.Groups["key"].Value;
                if (prefixes.Contains(key.Split('.')[0]))
                {
                    used.Add(key);
                }
            }
        }

        foreach (var file in SourceFiles("*.axaml"))
        {
            foreach (Match match in LocExtension().Matches(File.ReadAllText(file)))
            {
                used.Add(match.Groups["key"].Value);
            }
        }

        used.Should().NotBeEmpty();
        used.Where(k => !_catalog.Default.TryGet(k, out _)).Should().BeEmpty("every key the sources use must be in en.json");
    }

    /// <summary>
    /// The code and the views hold no Russian text: everything a user reads is in the packs.
    /// </summary>
    [Fact]
    public void Sources_HoldNoCyrillic()
    {
        var files = SourceFiles("*.cs").Concat(SourceFiles("*.axaml"));

        files.Where(f => Cyrillic().IsMatch(File.ReadAllText(f))).Should().BeEmpty();
    }

    /// <summary>
    /// Lists the placeholders of a text, such as <c>{0}</c> or <c>{1:0.0}</c>, by their number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The numbers of the placeholders.</returns>
    private static IEnumerable<string> Placeholders(string text)
    {
        return Placeholder().Matches(text).Select(m => m.Groups["n"].Value).Distinct().Order();
    }

    /// <summary>
    /// Lists the source files of the product.
    /// </summary>
    /// <param name="pattern">The file name pattern.</param>
    /// <returns>The full paths.</returns>
    private static IEnumerable<string> SourceFiles(string pattern)
    {
        var separator = Path.DirectorySeparatorChar;

        return Directory.EnumerateFiles(Path.Combine(Root(), "Sources"), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{separator}bin{separator}", StringComparison.Ordinal) &&
                !f.Contains($"{separator}obj{separator}", StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the root of the repository: the nearest folder above the test binaries that holds Voicer.slnx.
    /// </summary>
    /// <returns>The full path of the root.</returns>
    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Voicer.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Voicer.slnx was not found above the tests.");
    }

    /// <summary>
    /// A placeholder of a format string.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\{(?<n>\d+)(?:[:,][^}]*)?\}")]
    private static partial Regex Placeholder();

    /// <summary>
    /// A string literal that looks like a key: segments separated by dots.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"""(?<key>[A-Z][A-Za-z]+(?:\.[A-Za-z0-9][A-Za-z0-9\-]*)+)""")]
    private static partial Regex KeyLiteral();

    /// <summary>
    /// The localization markup extension of the views.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\{loc:Loc (?:Key=)?(?<key>[A-Za-z][\w.\-]*)")]
    private static partial Regex LocExtension();

    /// <summary>
    /// A Cyrillic letter.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\p{IsCyrillic}")]
    private static partial Regex Cyrillic();
}
