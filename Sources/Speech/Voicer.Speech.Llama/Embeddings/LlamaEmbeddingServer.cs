using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Voicer.Core.Embeddings;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Embeddings;

/// <summary>
/// <see cref="ILlamaEmbeddingServer"/> that runs one llama-server of the pinned build at a time: started by the first
/// search, kept while searches follow, and ended after a pause without requests, on a change of the model, when the
/// user stops it and with the application. A native failure ends only that process; the next search starts it again.
/// </summary>
public sealed partial class LlamaEmbeddingServer : ILlamaEmbeddingServer, IEmbeddingServerMonitor, IDisposable
{
    /// <summary>
    /// How long a server waits without searches before it is ended, giving its memory back.
    /// </summary>
    public static readonly TimeSpan DefaultIdleTime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a server may take to load its model and get ready.
    /// </summary>
    public static readonly TimeSpan DefaultStartTime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The path of the OpenAI-compatible API on the server.
    /// </summary>
    private const string API_PATH = "/v1";

    /// <summary>
    /// How many random bytes make the key of a server.
    /// </summary>
    private const int KEY_BYTES = 32;

    /// <summary>
    /// Finds llama-server.
    /// </summary>
    private readonly ILlamaCppLocator _locator;

    /// <summary>
    /// Where the models are.
    /// </summary>
    private readonly IEmbeddingModelStore _store;

    /// <summary>
    /// Starts the process.
    /// </summary>
    private readonly IServerProcessLauncher _launcher;

    /// <summary>
    /// The clock of the pause and of the start.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// How long a server waits without searches.
    /// </summary>
    private readonly TimeSpan _idleTime;

    /// <summary>
    /// How long a server may take to get ready.
    /// </summary>
    private readonly TimeSpan _startTime;

    /// <summary>
    /// Lets one caller at a time start or end the server.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Guards the state below, which the leases and the monitor read and change outside the gate too.
    /// </summary>
    private readonly Lock _sync = new();

    /// <summary>
    /// The running server, or <see langword="null"/>.
    /// </summary>
    private IServerProcess? _process;

    /// <summary>
    /// The model of the running or starting server.
    /// </summary>
    private EmbeddingModel? _model;

    /// <summary>
    /// The address of the running server.
    /// </summary>
    private LanguageModelEndpoint? _endpoint;

    /// <summary>
    /// The server loading its model, or <see langword="null"/>.
    /// </summary>
    private IServerProcess? _starting;

    /// <summary>
    /// How many leases are held: the server is not ended while any is.
    /// </summary>
    private int _leases;

    /// <summary>
    /// Ends the server after a pause.
    /// </summary>
    private ITimer? _idle;

    /// <summary>
    /// When the server ends by itself if no search comes, or <see langword="null"/> when it is not waiting.
    /// </summary>
    private DateTimeOffset? _stopsAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaEmbeddingServer"/> class.
    /// </summary>
    /// <param name="locator">Finds llama-server.</param>
    /// <param name="store">Where the models are.</param>
    /// <param name="launcher">Starts the process.</param>
    /// <param name="time">The clock of the pause and of the start.</param>
    /// <param name="idleTime">How long a server waits without searches; <see cref="DefaultIdleTime"/> when not given.</param>
    /// <param name="startTime">How long a server may take to get ready; <see cref="DefaultStartTime"/> when not given.</param>
    public LlamaEmbeddingServer(
        ILlamaCppLocator locator,
        IEmbeddingModelStore store,
        IServerProcessLauncher launcher,
        TimeProvider time,
        TimeSpan? idleTime = null,
        TimeSpan? startTime = null)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _idleTime = idleTime ?? DefaultIdleTime;
        _startTime = startTime ?? DefaultStartTime;
    }

    /// <inheritdoc />
    public async Task<EmbeddingServerLease> AcquireAsync(EmbeddingModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanguageModelEndpoint? endpoint;
            lock (_sync)
            {
                endpoint = _process is { HasExited: false } && _model == model ? _endpoint : null;
            }

            if (endpoint is null)
            {
                Stop();
                endpoint = await LaunchAsync(model, cancellationToken).ConfigureAwait(false);
            }

            lock (_sync)
            {
                _leases++;
                CancelPause();
            }

            return new EmbeddingServerLease(endpoint, Release);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public EmbeddingServerStatus Status()
    {
        IServerProcess? process;
        EmbeddingServerState state;
        string? name;
        DateTimeOffset? stopsAt;
        lock (_sync)
        {
            if (_starting is not null)
            {
                return new EmbeddingServerStatus(EmbeddingServerState.Starting, _model?.Name, null, null);
            }

            if (_process is null || _process.HasExited)
            {
                return EmbeddingServerStatus.Stopped;
            }

            process = _process;
            state = _leases > 0 ? EmbeddingServerState.Busy : EmbeddingServerState.Idle;
            name = _model?.Name;
            stopsAt = _stopsAt;
        }

        // The usage is read outside the lock: asking the system takes a moment.
        return new EmbeddingServerStatus(state, name, process.ReadUsage(), stopsAt);
    }

    /// <inheritdoc />
    public bool TryStop()
    {
        if (!_gate.Wait(0))
        {
            return false;
        }

        try
        {
            lock (_sync)
            {
                if (_leases > 0)
                {
                    return false;
                }
            }

            Stop();

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Ends the server, running or starting, whatever it is doing, without waiting for a start to finish: the
    /// application is closing.
    /// </summary>
    public void Dispose()
    {
        IServerProcess? starting;
        lock (_sync)
        {
            starting = _starting;
        }

        // A start that is waiting sees its process end and fails; the running server is ended here.
        starting?.Dispose();
        Stop();
    }

    /// <summary>
    /// Reads the address from the line of llama-server that says it listens.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The address of the server, without a path, or <see langword="null"/> when the line does not say it.</returns>
    internal static string? ReadAddress(string line)
    {
        var match = ListeningLine().Match(line);

        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Starts a server of a model and waits until it is ready; the caller holds the gate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">Stops the waiting.</param>
    /// <returns>The address of the server.</returns>
    private async Task<LanguageModelEndpoint> LaunchAsync(EmbeddingModel model, CancellationToken cancellationToken)
    {
        var program = _locator.LocateServer() ??
            throw LocalizedError.Create(m => new FileNotFoundException(m), "LlamaCpp.Missing");
        if (!_store.IsInstalled(model))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m), "Search.ModelMissing", model.Name);
        }

        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KEY_BYTES));
        var process = _launcher.Start(program, LlamaServerCommandLine.Build(_store.FileOf(model), model, key), line => ReadAddress(line) is not null);
        lock (_sync)
        {
            _starting = process;
            _model = model;
        }

        try
        {
            var ready = await WaitReadyAsync(process, model, cancellationToken).ConfigureAwait(false);
            if (ready is null || !LanguageModelEndpoint.TryCreate(ReadAddress(ready) + API_PATH, key, out var endpoint))
            {
                throw LocalizedError.Create(m => new InvalidDataException(m), "Search.ServerFailed", model.Name, process.LastWords);
            }

            lock (_sync)
            {
                _process = process;
                _endpoint = endpoint;
            }

            return endpoint;
        }
        catch
        {
            process.Dispose();
            lock (_sync)
            {
                _model = null;
            }

            throw;
        }
        finally
        {
            lock (_sync)
            {
                _starting = null;
            }
        }
    }

    /// <summary>
    /// Waits for the line that says the server is ready, at most for the time of a start.
    /// </summary>
    /// <param name="process">The starting server.</param>
    /// <param name="model">The model, for the text of a failure.</param>
    /// <param name="cancellationToken">Stops the waiting.</param>
    /// <returns>The line, or <see langword="null"/> when the process ended before it.</returns>
    private async Task<string?> WaitReadyAsync(IServerProcess process, EmbeddingModel model, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(_startTime, _time);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await process.Ready.WaitAsync(waiting.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw LocalizedError.Create(
                m => new InvalidDataException(m),
                "Search.ServerTimeout",
                model.Name,
                ((int)_startTime.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Takes a lease back; when it was the last one, the pause starts: the server is ended when it passes without
    /// another request.
    /// </summary>
    private void Release()
    {
        lock (_sync)
        {
            _leases--;
            if (_leases == 0 && _process is not null)
            {
                CancelPause();
                _stopsAt = _time.GetUtcNow() + _idleTime;
                _idle = _time.CreateTimer(_ => TryStop(), null, _idleTime, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>
    /// Forgets the pause; the caller holds the lock.
    /// </summary>
    private void CancelPause()
    {
        _idle?.Dispose();
        _idle = null;
        _stopsAt = null;
    }

    /// <summary>
    /// Ends the running server and forgets it; the caller holds the gate, or the application is closing.
    /// </summary>
    private void Stop()
    {
        IServerProcess? process;
        lock (_sync)
        {
            CancelPause();
            process = _process;
            _process = null;
            _model = null;
            _endpoint = null;
        }

        process?.Dispose();
    }

    /// <summary>
    /// Matches the line of llama-server that says where it listens.
    /// </summary>
    /// <returns>The expression; its first group is the address.</returns>
    [GeneratedRegex(@"listening on (http://127\.0\.0\.1:\d{1,5})\b", RegexOptions.CultureInvariant)]
    private static partial Regex ListeningLine();
}
