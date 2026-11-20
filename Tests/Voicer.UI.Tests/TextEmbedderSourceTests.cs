using FluentAssertions;
using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.Core.Search;
using Voicer.Core.Summaries;
using Voicer.Speech.Llama.Embeddings;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;
using Voicer.UI.Services.Search;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the model of the search by meaning the choice of the user gives.
/// </summary>
public sealed class TextEmbedderSourceTests
{
    /// <summary>
    /// The settings of a server with a model of embeddings chosen.
    /// </summary>
    private static readonly SummaryPreferences _withServerModel = SummaryPreferences.Default with { EmbeddingModel = "text-embedding-bge-m3" };

    /// <summary>
    /// The choice of the user.
    /// </summary>
    private readonly ISearchSourceStore _choice = Substitute.For<ISearchSourceStore>();

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models = Substitute.For<IEmbeddingModelStore>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _preferences = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// The source under test.
    /// </summary>
    private readonly TextEmbedderSource _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextEmbedderSourceTests"/> class: a model of Voicer is downloaded
    /// and the server has a model of embeddings, so only the choice decides.
    /// </summary>
    public TextEmbedderSourceTests()
    {
        _models.Preferred().Returns(EmbeddingModelCatalog.BgeM3);
        _preferences.Load().Returns(_withServerModel);
        _source = new TextEmbedderSource(_choice, _models, Substitute.For<ILlamaEmbeddingServer>(), _preferences, Substitute.For<ILanguageModelClient>());
    }

    /// <summary>
    /// The model of Voicer is used when it is chosen, though the server has a model too.
    /// </summary>
    [Fact]
    public void LocalChoice_UsesTheModelOfVoicer()
    {
        _choice.Load().Returns(SearchSource.Local);

        var embedder = _source.Current();

        embedder.Should().BeOfType<LocalTextEmbedder>();
        embedder!.Model.Should().Be("voicer/bge-m3");
    }

    /// <summary>
    /// The model of the server is used when it is chosen, though a model of Voicer is downloaded.
    /// </summary>
    [Fact]
    public void ServerChoice_UsesTheModelOfTheServer()
    {
        _choice.Load().Returns(SearchSource.Server);

        var embedder = _source.Current();

        embedder.Should().BeOfType<ServerTextEmbedder>();
        embedder!.Model.Should().Be("text-embedding-bge-m3");
    }

    /// <summary>
    /// Off means by words only, whatever is installed.
    /// </summary>
    [Fact]
    public void Off_SearchesByWords()
    {
        _choice.Load().Returns(SearchSource.Off);

        _source.Current().Should().BeNull();
    }

    /// <summary>
    /// A choice that is not ready yet searches by words, without taking the other source behind the back of the user.
    /// </summary>
    [Fact]
    public void ChoiceNotReady_SearchesByWords()
    {
        _choice.Load().Returns(SearchSource.Local);
        _models.Preferred().Returns((EmbeddingModel?)null);

        _source.Current().Should().BeNull();

        _choice.Load().Returns(SearchSource.Server);
        _preferences.Load().Returns(SummaryPreferences.Default);

        _source.Current().Should().BeNull();
    }
}
