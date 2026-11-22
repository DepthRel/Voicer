using FluentAssertions;
using NSubstitute;
using Voicer.Core.Summaries;
using Voicer.UI.Enums;
using Voicer.UI.Services.Configuration;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the remembered choice of what writes the summaries and the answers.
/// </summary>
public sealed class SummarySourceStoreTests
{
    /// <summary>
    /// The values of the settings file.
    /// </summary>
    private readonly Dictionary<string, string> _values = [];

    /// <summary>
    /// The models of Voicer.
    /// </summary>
    private readonly IChatModelStore _models = Substitute.For<IChatModelStore>();

    /// <summary>
    /// The settings of the server of language models.
    /// </summary>
    private readonly ISummaryPreferencesStore _server = Substitute.For<ISummaryPreferencesStore>();

    /// <summary>
    /// The store under test.
    /// </summary>
    private readonly SummarySourceStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="SummarySourceStoreTests"/> class.
    /// </summary>
    public SummarySourceStoreTests()
    {
        var settings = Substitute.For<ISettingsStore>();
        settings.Get(Arg.Any<string>()).Returns(call => _values.GetValueOrDefault(call.Arg<string>()));
        settings.When(s => s.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call => _values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        _server.Load().Returns(SummaryPreferences.Default);
        _store = new SummarySourceStore(settings, _models, _server);
    }

    /// <summary>
    /// The choice is kept under its own key, apart from the choice of the search.
    /// </summary>
    [Fact]
    public void Choice_IsKept()
    {
        _store.Save(ModelSource.Local);

        _store.Load().Should().Be(ModelSource.Local);
        _values["summary.source"].Should().Be("Local");
    }

    /// <summary>
    /// Before a choice, a server whose model was chosen keeps writing the summaries, as it did before the choice
    /// existed.
    /// </summary>
    [Fact]
    public void FirstChoice_KeepsAServerThatWasSetUp()
    {
        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        _server.Load().Returns(SummaryPreferences.Default with { Model = "qwen3-8b" });

        _store.Load().Should().Be(ModelSource.Server);
    }

    /// <summary>
    /// Without a server, a downloaded model of Voicer writes the summaries.
    /// </summary>
    [Fact]
    public void FirstChoice_TakesADownloadedModel()
    {
        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);

        _store.Load().Should().Be(ModelSource.Local);
    }

    /// <summary>
    /// The first guess is remembered at once, so what the settings show is what is used: a model downloaded or a server
    /// set up later does not turn the summaries on behind the back of the user.
    /// </summary>
    [Fact]
    public void FirstGuess_IsRemembered()
    {
        _store.Load().Should().Be(ModelSource.Off);
        _values["summary.source"].Should().Be("Off");

        _models.Preferred().Returns(ChatModelCatalog.Qwen3Small);
        _server.Load().Returns(SummaryPreferences.Default with { Model = "qwen3-8b" });

        _store.Load().Should().Be(ModelSource.Off);
    }
}
