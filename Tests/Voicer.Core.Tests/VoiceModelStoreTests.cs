using System.Text.RegularExpressions;
using FluentAssertions;
using Voicer.Core.Synthesis;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the store of the voice models and of the catalog it serves.
/// </summary>
public sealed partial class VoiceModelStoreTests : IDisposable
{
    /// <summary>
    /// A small model of two files with known sizes.
    /// </summary>
    private static readonly VoiceModel _model = new(
        "tiny-voice",
        "Tiny Voice",
        new VoiceModelFile("weights.gguf", new Uri("https://example.org/weights.gguf"), 4, "00"),
        new VoiceModelFile("mmproj.gguf", new Uri("https://example.org/mmproj.gguf"), 2, "00"),
        ["en"],
        [],
        CanCloneVoice: false,
        NeedsSample: false,
        RunsOnCpu: true);

    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-voices-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Removes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// A model is installed only when every one of its files is there with its size.
    /// </summary>
    [Fact]
    public void IsInstalled_NeedsEveryFile()
    {
        var store = new VoiceModelStore(_root, [_model]);
        Directory.CreateDirectory(store.DirectoryOf(_model));
        File.WriteAllBytes(store.FileOf(_model, _model.Weights), new byte[4]);

        store.IsInstalled(_model).Should().BeFalse();
        store.Installed().Should().BeEmpty();

        File.WriteAllBytes(store.FileOf(_model, _model.Projector), new byte[2]);

        store.IsInstalled(_model).Should().BeTrue();
        store.Installed().Should().Equal(_model);
        store.FileOf(_model, _model.Projector).Should().Be(Path.Combine(_root, "tiny-voice", "mmproj.gguf"));
    }

    /// <summary>
    /// Deleting a model removes every one of its files and its folder.
    /// </summary>
    [Fact]
    public void Delete_RemovesEveryFileAndTheFolder()
    {
        var store = new VoiceModelStore(_root, [_model]);
        Directory.CreateDirectory(store.DirectoryOf(_model));
        File.WriteAllBytes(store.FileOf(_model, _model.Weights), new byte[4]);
        File.WriteAllBytes(store.FileOf(_model, _model.Projector), new byte[2]);

        store.Delete(_model);

        store.IsInstalled(_model).Should().BeFalse();
        Directory.Exists(store.DirectoryOf(_model)).Should().BeFalse();
    }

    /// <summary>
    /// Every model of the catalog is pinned: HTTPS from a fixed revision, an exact size and a SHA-256; its folder name
    /// is safe, its default voice and language are its own.
    /// </summary>
    [Fact]
    public void Catalog_IsPinned()
    {
        VoiceModelCatalog.All.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        foreach (var model in VoiceModelCatalog.All)
        {
            model.Id.Should().MatchRegex("^[a-z0-9.-]+$");
            model.Languages.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
            model.Voices.Should().OnlyHaveUniqueItems();
            foreach (var file in model.Files)
            {
                file.DownloadUri.Scheme.Should().Be(Uri.UriSchemeHttps);
                file.DownloadUri.AbsolutePath.Should().MatchRegex("/resolve/[0-9a-f]{40}/");
                file.SizeBytes.Should().BePositive();
                Sha256().IsMatch(file.Sha256).Should().BeTrue(file.FileName);
            }
        }
    }

    /// <summary>
    /// The model the user asked for, and one that runs on the processor, are in the catalog.
    /// </summary>
    [Fact]
    public void Catalog_OffersQwen3Tts_AndAModelForTheProcessor()
    {
        VoiceModelCatalog.All.Should().Contain(VoiceModelCatalog.Qwen3Tts);
        VoiceModelCatalog.Qwen3Tts.CanCloneVoice.Should().BeTrue();
        VoiceModelCatalog.All.Should().Contain(m => m.RunsOnCpu);
    }

    /// <summary>
    /// A SHA-256 in lower-case hexadecimal.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256();
}
