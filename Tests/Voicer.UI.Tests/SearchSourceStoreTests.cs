using FluentAssertions;
using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the remembered choice of the source of the search by meaning.
/// </summary>
public sealed class SearchSourceStoreTests
{
    /// <summary>
    /// The settings of the user, kept in memory.
    /// </summary>
    private readonly Dictionary<string, string> _values = [];

    /// <summary>
    /// Where the models of Voicer live.
    /// </summary>
    private readonly IEmbeddingModelStore _models = Substitute.For<IEmbeddingModelStore>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// The store under test.
    /// </summary>
    private readonly SearchSourceStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchSourceStoreTests"/> class.
    /// </summary>
    public SearchSourceStoreTests()
    {
        var settings = Substitute.For<ISettingsStore>();
        settings.Get(Arg.Any<string>()).Returns(call => _values.GetValueOrDefault(call.Arg<string>()));
        settings.When(s => s.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call => _values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        _server.Load().Returns(SummaryPreferences.Default);
        _store = new SearchSourceStore(settings, _models, _server);
    }

    /// <summary>
    /// A choice made is kept, whatever is installed.
    /// </summary>
    [Fact]
    public void Choice_IsKept()
    {
        _models.Preferred().Returns(EmbeddingModelCatalog.BgeM3);

        _store.Save(SearchSource.Off);

        _store.Load().Should().Be(SearchSource.Off);
        _values["search.source"].Should().Be("Off");
    }

    /// <summary>
    /// Before the first choice the source follows what is there, so an update changes nothing: a downloaded model of
    /// Voicer first, then a model of embeddings of the server, then none.
    /// </summary>
    [Fact]
    public void FirstChoice_FollowsWhatIsThere()
    {
        _store.Load().Should().Be(SearchSource.Off);

        _server.Load().Returns(SummaryPreferences.Default with { EmbeddingModel = "text-embedding-bge-m3" });
        _store.Load().Should().Be(SearchSource.Server);

        _models.Preferred().Returns(EmbeddingModelCatalog.BgeM3);
        _store.Load().Should().Be(SearchSource.Local);
    }

    /// <summary>
    /// A value that is not a source, written by hand, is treated as no choice.
    /// </summary>
    [Fact]
    public void UnknownValue_IsNoChoice()
    {
        _values["search.source"] = "42";

        _store.Load().Should().Be(SearchSource.Off);
    }
}
