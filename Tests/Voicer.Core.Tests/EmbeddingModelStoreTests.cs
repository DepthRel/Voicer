using System.Text.RegularExpressions;
using FluentAssertions;
using Voicer.Core.Embeddings;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the store of the models of the search by meaning and of the catalog it serves.
/// </summary>
public sealed partial class EmbeddingModelStoreTests : IDisposable
{
    /// <summary>
    /// The preferred model of the tests: four bytes.
    /// </summary>
    private static readonly EmbeddingModel _large = Model("large-embed", 4);

    /// <summary>
    /// The other model of the tests: two bytes.
    /// </summary>
    private static readonly EmbeddingModel _small = Model("small-embed", 2);

    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-embeddings-" + Guid.NewGuid().ToString("N"));

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
    /// A model is installed only when its file has its full size; a part of it does not count.
    /// </summary>
    [Fact]
    public void IsInstalled_NeedsTheWholeFile()
    {
        var store = new EmbeddingModelStore(_root, [_large, _small]);

        Write(store, _large, 3);
        store.IsInstalled(_large).Should().BeFalse();

        Write(store, _large, 4);
        store.IsInstalled(_large).Should().BeTrue();
        store.FileOf(_large).Should().Be(Path.Combine(_root, "large-embed", "large-embed.gguf"));
    }

    /// <summary>
    /// The search uses the first installed model of the catalog, and none when nothing is installed.
    /// </summary>
    [Fact]
    public void Preferred_IsTheFirstInstalledOfTheCatalog()
    {
        var store = new EmbeddingModelStore(_root, [_large, _small]);
        store.Preferred().Should().BeNull();

        Write(store, _small, 2);
        store.Preferred().Should().Be(_small);

        Write(store, _large, 4);
        store.Preferred().Should().Be(_large);
    }

    /// <summary>
    /// Deleting a model removes its file and its folder.
    /// </summary>
    [Fact]
    public void Delete_RemovesTheFileAndTheFolder()
    {
        var store = new EmbeddingModelStore(_root, [_large]);
        Write(store, _large, 4);

        store.Delete(_large);

        store.IsInstalled(_large).Should().BeFalse();
        Directory.Exists(store.DirectoryOf(_large)).Should().BeFalse();
    }

    /// <summary>
    /// Every model of the catalog is pinned: HTTPS from a fixed revision, an exact size and a SHA-256; its folder name
    /// is safe, and its vectors are kept apart from those of a server.
    /// </summary>
    [Fact]
    public void Catalog_IsPinned()
    {
        EmbeddingModelCatalog.All.Should().NotBeEmpty();
        EmbeddingModelCatalog.All.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        foreach (var model in EmbeddingModelCatalog.All)
        {
            model.Id.Should().MatchRegex("^[a-z0-9.-]+$");
            model.DownloadUri.Scheme.Should().Be(Uri.UriSchemeHttps);
            model.DownloadUri.AbsolutePath.Should().MatchRegex("/resolve/[0-9a-f]{40}/");
            model.SizeBytes.Should().BePositive();
            model.SlotTokens.Should().BePositive();
            Sha256().IsMatch(model.Sha256).Should().BeTrue(model.FileName);
            model.VectorName.Should().Be("voicer/" + model.Id);
        }
    }

    /// <summary>
    /// Creates a model of the tests.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="size">The size of its file.</param>
    /// <returns>The model.</returns>
    private static EmbeddingModel Model(string id, long size)
    {
        return new EmbeddingModel(id, id, id + ".gguf", new Uri("https://example.org/" + id), size, new string('a', 64), 512);
    }

    /// <summary>
    /// Writes the file of a model of a given length.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="model">The model.</param>
    /// <param name="length">The length.</param>
    private static void Write(EmbeddingModelStore store, EmbeddingModel model, int length)
    {
        Directory.CreateDirectory(store.DirectoryOf(model));
        File.WriteAllBytes(store.FileOf(model), new byte[length]);
    }

    /// <summary>
    /// A SHA-256 in lower-case hexadecimal.
    /// </summary>
    /// <returns>The expression.</returns>
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256();
}
