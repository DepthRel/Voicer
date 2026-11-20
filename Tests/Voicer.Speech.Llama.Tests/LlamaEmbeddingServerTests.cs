using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Voicer.Core.Embeddings;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Embeddings;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Tests;

/// <summary>
/// Tests of the llama-server of the search by meaning: its start, its arguments, its reuse, its end after a pause and
/// the failures of its start.
/// </summary>
public sealed class LlamaEmbeddingServerTests : IDisposable
{
    /// <summary>
    /// The line llama-server writes when it is ready, as the pinned build writes it.
    /// </summary>
    private const string READY_LINE = "0.02.203.058 I srv  server_on_li: listening on http://127.0.0.1:54230";

    /// <summary>
    /// The path of the program in the tests.
    /// </summary>
    private const string PROGRAM = "llama-server";

    /// <summary>
    /// How long a server waits without searches in the tests.
    /// </summary>
    private static readonly TimeSpan _idle = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a server may take to start in the tests.
    /// </summary>
    private static readonly TimeSpan _start = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The model of the tests.
    /// </summary>
    private static readonly EmbeddingModel _model = EmbeddingModelCatalog.BgeM3;

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _locator = Substitute.For<ILlamaCppLocator>();

    /// <summary>
    /// Where the models are.
    /// </summary>
    private readonly IEmbeddingModelStore _store = Substitute.For<IEmbeddingModelStore>();

    /// <summary>
    /// Starts the processes, the fake ones of the tests.
    /// </summary>
    private readonly IServerProcessLauncher _launcher = Substitute.For<IServerProcessLauncher>();

    /// <summary>
    /// The clock of the tests.
    /// </summary>
    private readonly FakeTimeProvider _time = new();

    /// <summary>
    /// The processes started, in their order.
    /// </summary>
    private readonly List<FakeProcess> _started = [];

    /// <summary>
    /// The arguments of the last start.
    /// </summary>
    private IReadOnlyList<string> _arguments = [];

    /// <summary>
    /// Tells a ready line, as the server under test gave it to the last start.
    /// </summary>
    private Func<string, bool> _isReady = _ => false;

    /// <summary>
    /// What the next process started does.
    /// </summary>
    private Func<FakeProcess> _next = () => FakeProcess.ReadyAt(READY_LINE);

    /// <summary>
    /// The server under test.
    /// </summary>
    private readonly LlamaEmbeddingServer _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaEmbeddingServerTests"/> class.
    /// </summary>
    public LlamaEmbeddingServerTests()
    {
        _locator.LocateServer().Returns(PROGRAM);
        _store.IsInstalled(Arg.Any<EmbeddingModel>()).Returns(true);
        _store.FileOf(Arg.Any<EmbeddingModel>()).Returns(call => "models/" + call.Arg<EmbeddingModel>().FileName);
        _launcher.Start(default!, default!, default!).ReturnsForAnyArgs(call =>
        {
            _arguments = call.ArgAt<IReadOnlyList<string>>(1);
            _isReady = call.ArgAt<Func<string, bool>>(2);
            var process = _next();
            _started.Add(process);

            return process;
        });
        _server = new LlamaEmbeddingServer(_locator, _store, _launcher, _time, _idle, _start);
    }

    /// <summary>
    /// Ends the server under test.
    /// </summary>
    public void Dispose()
    {
        _server.Dispose();
    }

    /// <summary>
    /// The first search starts the server with the model, only on this computer, on a free port, with a key of its own
    /// and without a web page, and learns its address from the line that says it listens.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Start_LaunchesTheServerOfTheModel()
    {
        var endpoint = await UseAsync(_model, CancellationToken.None);

        endpoint.BaseAddress.AbsoluteUri.Should().Be("http://127.0.0.1:54230/v1");
        endpoint.ApiKey.Should().MatchRegex("^[0-9a-f]{64}$");
        _launcher.Received(1).Start(PROGRAM, Arg.Any<IReadOnlyList<string>>(), Arg.Any<Func<string, bool>>());
        _arguments.Should().ContainInConsecutiveOrder("-m", "models/" + _model.FileName);
        _arguments.Should().ContainInConsecutiveOrder("--alias", _model.Id);
        _arguments.Should().ContainInConsecutiveOrder("--host", "127.0.0.1");
        _arguments.Should().ContainInConsecutiveOrder("--port", "0");
        _arguments.Should().ContainInConsecutiveOrder("--api-key", endpoint.ApiKey!);
        _arguments.Should().Contain(["--embedding", "--no-webui"]);
        _arguments.Should().ContainInConsecutiveOrder("--ubatch-size", "2048");
        _isReady(READY_LINE).Should().BeTrue();
        _isReady("0.01.004.076 E srv    load_model: failed to load model").Should().BeFalse();
    }

    /// <summary>
    /// Searches that follow one another use the running server.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Start_Again_UsesTheRunningServer()
    {
        var first = await UseAsync(_model, CancellationToken.None);
        var second = await UseAsync(_model, CancellationToken.None);

        second.Should().Be(first);
        _started.Should().ContainSingle();
    }

    /// <summary>
    /// A server that waited long enough without searches is ended; a search meanwhile starts the pause again, and the
    /// next search after the end starts a new server.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Pause_EndsTheServer()
    {
        await UseAsync(_model, CancellationToken.None);
        _time.Advance(_idle - TimeSpan.FromSeconds(1));
        await UseAsync(_model, CancellationToken.None);
        _time.Advance(_idle - TimeSpan.FromSeconds(1));

        _started[0].IsDisposed.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(2));

        _started[0].IsDisposed.Should().BeTrue();

        await UseAsync(_model, CancellationToken.None);

        _started.Should().HaveCount(2);
    }

    /// <summary>
    /// A request that takes longer than the pause is not cut: the pause starts only when the request is over.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Pause_WaitsForTheRequestInProgress()
    {
        var lease = await _server.AcquireAsync(_model, CancellationToken.None);
        _time.Advance(_idle * 3);

        _started[0].IsDisposed.Should().BeFalse();

        lease.Dispose();
        lease.Dispose();
        _time.Advance(_idle - TimeSpan.FromSeconds(1));

        _started[0].IsDisposed.Should().BeFalse();

        _time.Advance(TimeSpan.FromSeconds(2));

        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// A server that ended by itself (a native failure) is started again by the next search.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task EndedServer_IsStartedAgain()
    {
        await UseAsync(_model, CancellationToken.None);
        _started[0].HasExited = true;

        await UseAsync(_model, CancellationToken.None);

        _started.Should().HaveCount(2);
        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Another model ends the server of the first one before its own starts.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task AnotherModel_EndsTheFirstServer()
    {
        var other = _model with { Id = "other-embed", FileName = "other.gguf" };
        await UseAsync(_model, CancellationToken.None);

        await UseAsync(other, CancellationToken.None);

        _started.Should().HaveCount(2);
        _started[0].IsDisposed.Should().BeTrue();
        _arguments.Should().ContainInConsecutiveOrder("-m", "models/other.gguf");
    }

    /// <summary>
    /// A server that ends before it is ready is reported with what it said last, and nothing is left running.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ServerThatFails_IsReportedWithItsLastWords()
    {
        _next = () => FakeProcess.EndedWith("error loading model: bert model needs to define token type count");

        var start = () => UseAsync(_model, CancellationToken.None);

        var failure = await start.Should().ThrowAsync<InvalidDataException>();
        TestTexts.English.Describe(failure.Which).Should().Be(
            TestTexts.English.Format("Search.ServerFailed", _model.Name, "error loading model: bert model needs to define token type count"));
        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// A server that does not get ready in time is ended and reported.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task ServerThatHangs_IsEndedAfterTheTime()
    {
        _next = () => new FakeProcess();

        var start = UseAsync(_model, CancellationToken.None);
        _time.Advance(_start);

        var failure = await ((Func<Task>)(() => start)).Should().ThrowAsync<InvalidDataException>();
        TestTexts.English.Describe(failure.Which).Should().Be(TestTexts.English.Format("Search.ServerTimeout", _model.Name, "120"));
        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// A search stopped while the server starts ends the server.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task StoppedSearch_EndsTheStartingServer()
    {
        _next = () => new FakeProcess();
        using var stop = new CancellationTokenSource();

        var start = UseAsync(_model, stop.Token);
        await stop.CancelAsync();

        await ((Func<Task>)(() => start)).Should().ThrowAsync<OperationCanceledException>();
        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Without llama-server or without the model nothing is started, and the user is told what is missing.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task MissingParts_AreReported()
    {
        _store.IsInstalled(_model).Returns(false);

        var withoutModel = await ((Func<Task>)(() => UseAsync(_model, CancellationToken.None))).Should().ThrowAsync<FileNotFoundException>();
        TestTexts.English.Describe(withoutModel.Which).Should().Be(TestTexts.English.Format("Search.ModelMissing", _model.Name));

        _locator.LocateServer().Returns((string?)null);

        var withoutServer = await ((Func<Task>)(() => UseAsync(_model, CancellationToken.None))).Should().ThrowAsync<FileNotFoundException>();
        TestTexts.English.Describe(withoutServer.Which).Should().Be(TestTexts.English.Get("LlamaCpp.Missing"));
        _started.Should().BeEmpty();
    }

    /// <summary>
    /// Ending the application ends the server.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Dispose_EndsTheServer()
    {
        await UseAsync(_model, CancellationToken.None);

        _server.Dispose();

        _started[0].IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// The state follows the server: stopped, starting with its model, busy while a search holds it with what its
    /// process takes, then waiting with the moment it stops by itself.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Status_FollowsTheServer()
    {
        var starting = new FakeProcess();
        _next = () => starting;
        _server.Status().Should().Be(EmbeddingServerStatus.Stopped);

        var acquiring = _server.AcquireAsync(_model, CancellationToken.None);

        _server.Status().Should().Be(new EmbeddingServerStatus(EmbeddingServerState.Starting, _model.Name, null, null));

        starting.MakeReady(READY_LINE);
        var lease = await acquiring;

        _server.Status().Should().Be(new EmbeddingServerStatus(EmbeddingServerState.Busy, _model.Name, starting.Usage, null));

        lease.Dispose();

        _server.Status().Should().Be(new EmbeddingServerStatus(EmbeddingServerState.Idle, _model.Name, starting.Usage, _time.GetUtcNow() + _idle));
    }

    /// <summary>
    /// The user stops a waiting server at once; a server that a search holds, or that is starting, is left alone.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task TryStop_StopsOnlyAServerNotInUse()
    {
        var lease = await _server.AcquireAsync(_model, CancellationToken.None);

        _server.TryStop().Should().BeFalse();
        _started[0].IsDisposed.Should().BeFalse();

        lease.Dispose();

        _server.TryStop().Should().BeTrue();
        _started[0].IsDisposed.Should().BeTrue();
        _server.Status().Should().Be(EmbeddingServerStatus.Stopped);

        _next = () => new FakeProcess();
        var starting = _server.AcquireAsync(_model, CancellationToken.None);

        _server.TryStop().Should().BeFalse();
        _started[1].IsDisposed.Should().BeFalse();

        _started[1].MakeReady(READY_LINE);
        (await starting).Dispose();
    }

    /// <summary>
    /// Closing the application while a model loads ends the starting server at once, without waiting for it.
    /// </summary>
    /// <returns>A task that completes with the test.</returns>
    [Fact]
    public async Task Dispose_EndsAStartingServerAtOnce()
    {
        _next = () => new FakeProcess();
        var starting = _server.AcquireAsync(_model, CancellationToken.None);

        _server.Dispose();

        _started[0].IsDisposed.Should().BeTrue();
        await ((Func<Task>)(() => starting)).Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>
    /// Only an address of this computer is taken from the output.
    /// </summary>
    [Fact]
    public void ReadAddress_TakesOnlyALocalAddress()
    {
        LlamaEmbeddingServer.ReadAddress(READY_LINE).Should().Be("http://127.0.0.1:54230");
        LlamaEmbeddingServer.ReadAddress("listening on http://0.0.0.0:8080").Should().BeNull();
        LlamaEmbeddingServer.ReadAddress("load_model: loading model").Should().BeNull();
    }

    /// <summary>
    /// Makes one request of the server under test: takes it and gives it back at once.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">Stops the waiting for the start.</param>
    /// <returns>The address the request was given.</returns>
    private async Task<LanguageModelEndpoint> UseAsync(EmbeddingModel model, CancellationToken cancellationToken)
    {
        using var lease = await _server.AcquireAsync(model, cancellationToken);

        return lease.Endpoint;
    }

    /// <summary>
    /// A process of the tests: ready when told, ended when told, and remembers that it was ended.
    /// </summary>
    private sealed class FakeProcess : IServerProcess
    {
        /// <summary>
        /// Completes when the process is ready or ended.
        /// </summary>
        private readonly TaskCompletionSource<string?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc />
        public Task<string?> Ready => _ready.Task;

        /// <inheritdoc />
        public bool HasExited { get; set; }

        /// <inheritdoc />
        public string LastWords { get; private set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the process was ended.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Gets what the process takes while it runs.
        /// </summary>
        public ServerUsage Usage { get; } = new(4242, 700_000_000, TimeSpan.FromSeconds(3));

        /// <inheritdoc />
        public ServerUsage? ReadUsage()
        {
            return HasExited ? null : Usage;
        }

        /// <summary>
        /// Creates a process that is ready at once with a line.
        /// </summary>
        /// <param name="line">The line that says it is ready.</param>
        /// <returns>The process.</returns>
        public static FakeProcess ReadyAt(string line)
        {
            var process = new FakeProcess();
            process._ready.SetResult(line);

            return process;
        }

        /// <summary>
        /// Makes the process ready with a line.
        /// </summary>
        /// <param name="line">The line that says it is ready.</param>
        public void MakeReady(string line)
        {
            _ready.SetResult(line);
        }

        /// <summary>
        /// Creates a process that ended before it got ready.
        /// </summary>
        /// <param name="lastWords">What it said last.</param>
        /// <returns>The process.</returns>
        public static FakeProcess EndedWith(string lastWords)
        {
            var process = new FakeProcess { HasExited = true, LastWords = lastWords };
            process._ready.SetResult(null);

            return process;
        }

        /// <summary>
        /// Ends the process: like a real one, a process ended before it got ready is never ready.
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
            HasExited = true;
            _ready.TrySetResult(null);
        }
    }
}
