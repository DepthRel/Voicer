using FluentAssertions;
using NSubstitute;
using Voicer.Core.Search;
using Voicer.UI.Services.History;
using Voicer.UI.Services.Search;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Tests;

/// <summary>
/// Tests of the search of the page of the history: the passages, the answer with its sources, and the opening of a
/// record at a passage.
/// </summary>
public sealed class HistorySearchViewModelTests
{
    /// <summary>
    /// A passage of a meeting, from the twelfth second.
    /// </summary>
    private static readonly PassageHit _hit = new(4, 9, "meeting.mp4", DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(40), "Бюджет утвердили.");

    /// <summary>
    /// Searches and answers.
    /// </summary>
    private readonly IRecordSearchService _service = Substitute.For<IRecordSearchService>();

    /// <summary>
    /// Opens the records.
    /// </summary>
    private readonly IRecordViewer _viewer = Substitute.For<IRecordViewer>();

    /// <summary>
    /// The search under test, in Russian.
    /// </summary>
    private readonly HistorySearchViewModel _search;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistorySearchViewModelTests"/> class.
    /// </summary>
    public HistorySearchViewModelTests()
    {
        _search = new HistorySearchViewModel(TestTexts.Russian, _service, _viewer) { Query = "  бюджет  " };
    }

    /// <summary>
    /// The passages found are shown with their moment, and the status says how they were found.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Search_ShowsThePassages()
    {
        _service.SearchAsync("бюджет", Arg.Any<IProgress<SearchProgress>?>(), Arg.Any<CancellationToken>()).Returns([_hit]);
        _service.SearchesByMeaning.Returns(true);

        await _search.SearchCommand.ExecuteAsync(null);

        _search.IsActive.Should().BeTrue();
        _search.Hits.Should().ContainSingle();
        _search.Hits[0].HasNumber.Should().BeFalse();
        _search.Hits[0].Details.Should().Be(TestTexts.Russian.Format("Search.HitDetails", DateTimeOffset.UnixEpoch.ToString("g", TestTexts.Russian.Culture), "0:12", "0:40"));
        _search.Status.Should().Be(TestTexts.Russian.Format("Search.Found", 1));
        _search.HasAnswer.Should().BeFalse();
    }

    /// <summary>
    /// The answer of the model is shown with its numbered sources.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Ask_ShowsTheAnswerWithItsSources()
    {
        _service.CanAsk.Returns(true);
        _service.AskAsync("бюджет", Arg.Any<IProgress<SearchProgress>?>(), Arg.Any<IProgress<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new QuestionAnswer("Утвердили [1].", [_hit]));

        await _search.AskCommand.ExecuteAsync(null);

        _search.Answer.Should().Be("Утвердили [1].");
        _search.Hits.Single().NumberText.Should().Be("[1]");
        _search.HasError.Should().BeFalse();
    }

    /// <summary>
    /// Without a model that answers, the user is told to choose one.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Ask_WithoutAModel_SaysSo()
    {
        await _search.AskCommand.ExecuteAsync(null);

        _search.Error.Should().Be(TestTexts.Russian.Get("Summary.NotConfigured"));
        await _service.DidNotReceiveWithAnyArgs().AskAsync(default!, default, default, default);
    }

    /// <summary>
    /// A failed search says why and shows no answer.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Search_ThatFailed_SaysWhy()
    {
        _service.SearchAsync(default!, default, default).ReturnsForAnyArgs(Task.FromException<IReadOnlyList<PassageHit>>(new HttpRequestException("refused")));

        await _search.SearchCommand.ExecuteAsync(null);

        _search.Error.Should().Be(TestTexts.Russian.Format("Search.Failed", "refused"));
        _search.IsWorking.Should().BeFalse();
    }

    /// <summary>
    /// A passage opens its record at its moment; going back to the list forgets the results.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Open_PlaysTheRecordFromThePassage()
    {
        _service.SearchAsync(default!, default, default).ReturnsForAnyArgs([_hit]);
        _viewer.OpenAsync(9, Arg.Any<CancellationToken>(), TimeSpan.FromSeconds(12)).Returns(true);
        await _search.SearchCommand.ExecuteAsync(null);

        await _search.Hits[0].OpenCommand.ExecuteAsync(null);
        _search.ClearCommand.Execute(null);

        await _viewer.Received(1).OpenAsync(9, Arg.Any<CancellationToken>(), TimeSpan.FromSeconds(12));
        _search.IsActive.Should().BeFalse();
        _search.Hits.Should().BeEmpty();
    }
}
