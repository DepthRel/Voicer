using System.Text.RegularExpressions;
using FluentAssertions;
using Voicer.Core.Summaries;

namespace Voicer.Core.Tests;

/// <summary>
/// Tests of the store of the models of Voicer that write summaries and answers, and of their catalog.
/// </summary>
public sealed partial class ChatModelStoreTests : IDisposable
{
    /// <summary>
    /// A temporary models folder.
    /// </summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "voicer-chat-" + Guid.NewGuid().ToString("N"));

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
    /// The summaries use the first installed model of the catalog: the stronger one when both are there.
    /// </summary>
    [Fact]
    public void Preferred_IsTheFirstInstalledOfTheCatalog()
    {
        var strong = Model("strong", 4);
        var light = Model("light", 2);
        var store = new ChatModelStore(_root, [strong, light]);
        store.Preferred().Should().BeNull();

        Write(store, light, 2);
        store.Preferred().Should().Be(light);

        Write(store, strong, 3);
        store.Preferred().Should().Be(light, "a part of a file is not a model");

        Write(store, strong, 4);
        store.Preferred().Should().Be(strong);
        store.FileOf(strong).Should().Be(Path.Combine(_root, "strong", "strong.gguf"));
    }

    /// <summary>
    /// Every model of the catalog is pinned: HTTPS from a fixed revision, an exact size and a SHA-256, a safe folder
    /// name, a context the summaries can work with, and a key of its description.
    /// </summary>
    [Fact]
    public void Catalog_IsPinned()
    {
        ChatModelCatalog.All.Should().NotBeEmpty();
        ChatModelCatalog.All.Select(m => m.Id).Should().OnlyHaveUniqueItems();
        foreach (var model in ChatModelCatalog.All)
        {
            model.Id.Should().MatchRegex("^[a-z0-9.-]+$");
            model.DownloadUri.Scheme.Should().Be(Uri.UriSchemeHttps);
            model.DownloadUri.AbsolutePath.Should().MatchRegex("/resolve/[0-9a-f]{40}/");
            model.SizeBytes.Should().BePositive();
            model.ContextTokens.Should().BeInRange(SummarySettings.MIN_CONTEXT_TOKENS, SummarySettings.MAX_CONTEXT_TOKENS);
            Sha256().IsMatch(model.Sha256).Should().BeTrue(model.FileName);
            model.DescriptionKey.Should().Be($"ChatModel.{model.Id}.Description");
        }
    }

    /// <summary>
    /// Creates a model of the tests.
    /// </summary>
    /// <param name="id">Its identifier.</param>
    /// <param name="size">The size of its file.</param>
    /// <returns>The model.</returns>
    private static ChatModel Model(string id, long size)
    {
        return new ChatModel(id, id, id + ".gguf", new Uri("https://example.org/" + id), size, new string('a', 64), 4096, Thinks: false);
    }

    /// <summary>
    /// Writes the file of a model of a given length.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="model">The model.</param>
    /// <param name="length">The length.</param>
    private static void Write(ChatModelStore store, ChatModel model, int length)
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
