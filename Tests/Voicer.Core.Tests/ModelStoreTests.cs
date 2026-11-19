using System.Text.RegularExpressions;
using FluentAssertions;
using Voicer.Core.Enums;
using Voicer.Core.Models;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the store of the downloaded models and of the catalog it serves.
/// </summary>
public sealed class ModelStoreTests : IDisposable
{
    /// <summary>
    /// A small model with a known size.
    /// </summary>
    private static readonly SpeechModel _model = new(
        "tiny-test-f16",
        "tiny-test",
        "Tiny",
        ModelQuantization.F16,
        "model.bin",
        new Uri("https://example.org/model.bin"),
        4,
        "00");

    /// <summary>
    /// The same small model in another quantization.
    /// </summary>
    private static readonly SpeechModel _quantized = _model with { Id = "tiny-test-q5", Quantization = ModelQuantization.Q5, FileName = "model-q5.bin", SizeBytes = 2 };

    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-models-" + Guid.NewGuid().ToString("N"));

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
    /// A model lives in the folder of its family, next to its other quantizations.
    /// </summary>
    [Fact]
    public void FileOf_IsInTheFolderOfTheFamily()
    {
        var store = new ModelStore(_root, [_model, _quantized]);

        store.FileOf(_model).Should().Be(Path.Combine(_root, "tiny-test", "model.bin"));
        store.FileOf(_quantized).Should().Be(Path.Combine(_root, "tiny-test", "model-q5.bin"));
    }

    /// <summary>
    /// A model is installed only when its file has the expected size; a partial file does not count.
    /// </summary>
    [Fact]
    public void IsInstalled_NeedsTheWholeFile()
    {
        var store = new ModelStore(_root, [_model]);
        Directory.CreateDirectory(store.DirectoryOf(_model));

        store.IsInstalled(_model).Should().BeFalse();
        File.WriteAllBytes(store.FileOf(_model), [1, 2]);
        store.IsInstalled(_model).Should().BeFalse();
        File.WriteAllBytes(store.FileOf(_model), [1, 2, 3, 4]);
        store.IsInstalled(_model).Should().BeTrue();
        store.Installed().Should().Equal(_model);
    }

    /// <summary>
    /// Every quantization of a family counts on its own: one downloaded file does not make the others installed.
    /// </summary>
    [Fact]
    public void Installed_TellsQuantizationsApart()
    {
        var store = new ModelStore(_root, [_model, _quantized]);
        Directory.CreateDirectory(store.DirectoryOf(_quantized));

        File.WriteAllBytes(store.FileOf(_quantized), [1, 2]);

        store.IsInstalled(_model).Should().BeFalse();
        store.Installed().Should().Equal(_quantized);
    }

    /// <summary>
    /// The model downloaded before quantizations were offered, <c>Data/Models/whisper-large-v3/ggml-large-v3.bin</c>,
    /// is still found where it is.
    /// </summary>
    [Fact]
    public void Catalog_KeepsTheFileOfLargeV3WhereItWas()
    {
        var store = new ModelStore(_root);

        store.FileOf(SpeechModelCatalog.WhisperLargeV3).Should().Be(Path.Combine(_root, "whisper-large-v3", "ggml-large-v3.bin"));
        SpeechModelCatalog.WhisperLargeV3.Quantization.Should().Be(ModelQuantization.F16);
        SpeechModelCatalog.Find(SpeechModelCatalog.WhisperLargeV3.Id).Should().Be(SpeechModelCatalog.WhisperLargeV3);
    }

    /// <summary>
    /// Every model of the catalog is pinned by size and SHA-256, downloaded over HTTPS, and has an identifier and a file
    /// of its own.
    /// </summary>
    [Fact]
    public void Catalog_PinsEveryModel()
    {
        var models = SpeechModelCatalog.All;

        models.Should().OnlyContain(m => m.DownloadUri.Scheme == Uri.UriSchemeHttps);
        models.Should().OnlyContain(m => m.SizeBytes > 0);
        models.Select(m => m.Sha256).Should().OnlyContain(h => Regex.IsMatch(h, "^[0-9a-f]{64}$"));
        models.Select(m => m.Id).Should().OnlyHaveUniqueItems().And.OnlyContain(id => Regex.IsMatch(id, "^[a-z0-9-]+$"));
        models.Select(m => m.Family).Should().OnlyContain(f => Regex.IsMatch(f, "^[a-z0-9-]+$"));
        models.Select(m => m.Sha256).Should().OnlyHaveUniqueItems();
        models.Select(m => (m.Family, m.FileName)).Should().OnlyHaveUniqueItems();
        models.Select(m => (m.Family, m.Quantization)).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The families go from the heaviest to the lightest, and inside a family from the most accurate quantization to the
    /// smallest; the files of a family stand together.
    /// </summary>
    [Fact]
    public void Catalog_GoesFromTheHeaviestToTheLightest()
    {
        var families = SpeechModelCatalog.All.GroupBy(m => m.Family).ToList();

        families.Select(f => f.Key).Should().Equal("whisper-large-v3", "whisper-large-v3-turbo", "whisper-medium", "whisper-small");
        SpeechModelCatalog.All.Select(m => m.Family).Should().Equal(families.SelectMany(f => f.Select(m => m.Family)));
        families.Select(f => f.Max(m => m.SizeBytes)).Should().BeInDescendingOrder();
        foreach (var family in families)
        {
            family.Select(m => m.Quantization).Should().BeInAscendingOrder();
            family.Select(m => m.SizeBytes).Should().BeInDescendingOrder();
        }
    }
}
