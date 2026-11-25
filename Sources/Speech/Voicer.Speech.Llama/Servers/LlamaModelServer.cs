using System.Globalization;
using System.Security.Cryptography;
using Voicer.Core.Models;
using Voicer.Core.Summaries;
using Voicer.Localization;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// <see cref="ILlamaModelServer{TModel}"/> that runs one llama-server of the pinned build at a time: started by the
/// first request, kept while requests follow, and ended after a pause without requests, on a change of the model, when
/// the user stops it and with the application. A native failure ends only that process; the next request starts it
/// again.
/// </summary>
/// <typeparam name="TModel">The kind of the models.</typeparam>
public abstract class LlamaModelServer<TModel> : ILlamaModelServer<TModel>, ILlamaServerMonitor, IDisposable
    where TModel : class, IServerModel
{
    /// <summary>
    /// How long a server waits without requests before it is ended, giving its memory back.
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
    private readonly IServerModelStore<TModel> _store;

    /// <summary>
    /// Starts the process.
    /// </summary>
    private readonly IServerProcessLauncher _launcher;

    /// <summary>
    /// The clock of the pause and of the start.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// How long a server waits without requests.
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
    private TModel? _model;

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
    /// How many times the server was stopped by the user: a start that sees it change was unloaded on the way.
    /// </summary>
    private int _stops;

    /// <summary>
    /// Ends the server after a pause.
    /// </summary>
    private ITimer? _idle;

    /// <summary>
    /// When the server ends by itself if no request comes, or <see langword="null"/> when it is not waiting.
    /// </summary>
    private DateTimeOffset? _stopsAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaModelServer{TModel}"/> class.
    /// </summary>
    /// <param name="locator">Finds llama-server.</param>
    /// <param name="store">Where the models are.</param>
    /// <param name="launcher">Starts the process.</param>
    /// <param name="time">The clock of the pause and of the start.</param>
    /// <param name="idleTime">How long a server waits without requests; <see cref="DefaultIdleTime"/> when not given.</param>
    /// <param name="startTime">How long a server may take to get ready; <see cref="DefaultStartTime"/> when not given.</param>
    protected LlamaModelServer(
        ILlamaCppLocator locator,
        IServerModelStore<TModel> store,
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
    public async Task<LlamaServerLease> AcquireAsync(TModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LanguageModelEndpoint? endpoint;
            lock (_sync)
            {
                endpoint = _process is { HasExited: false } && Equals(_model, model) ? _endpoint : null;
            }

            if (endpoint is null)
            {
                End();
                endpoint = await LaunchAsync(model, cancellationToken).ConfigureAwait(false);
            }

            lock (_sync)
            {
                _leases++;
                CancelPause();
            }

            return new LlamaServerLease(endpoint, Release);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public LlamaServerStatus Status()
    {
        IServerProcess? process;
        LlamaServerState state;
        string? name;
        DateTimeOffset? stopsAt;
        lock (_sync)
        {
            if (_starting is not null)
            {
                return new LlamaServerStatus(LlamaServerState.Starting, _model?.Name, null, null);
            }

            if (_process is null || _process.HasExited)
            {
                return LlamaServerStatus.Stopped;
            }

            process = _process;
            state = _leases > 0 ? LlamaServerState.Busy : LlamaServerState.Idle;
            name = _model?.Name;
            stopsAt = _stopsAt;
        }

        // The usage is read outside the lock: asking the system takes a moment.
        return new LlamaServerStatus(state, name, process.ReadUsage(), stopsAt);
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

            End();

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        IServerProcess? starting;
        lock (_sync)
        {
            _stops++;
            starting = _starting;
        }

        // A start that is waiting sees its process end and fails; the running server is ended here.
        starting?.Dispose();
        End();
    }

    /// <summary>
    /// Ends the server, running or starting, whatever it is doing: the application is closing.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds the arguments of llama-server for a model.
    /// </summary>
    /// <param name="modelFile">The file of the weights of the model.</param>
    /// <param name="model">The model.</param>
    /// <returns>The arguments, without a shell.</returns>
    protected abstract IReadOnlyList<string> Arguments(string modelFile, TModel model);

    /// <summary>
    /// Starts a server of a model and waits until it is ready; the caller holds the gate.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="cancellationToken">Stops the waiting.</param>
    /// <returns>The address of the server.</returns>
    private async Task<LanguageModelEndpoint> LaunchAsync(TModel model, CancellationToken cancellationToken)
    {
        var program = _locator.LocateServer() ??
            throw LocalizedError.Create(m => new FileNotFoundException(m), "LlamaCpp.Missing");
        if (!_store.IsInstalled(model))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m), "LlamaServer.ModelMissing", model.Name);
        }

        int stops;
        lock (_sync)
        {
            stops = _stops;
        }

        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KEY_BYTES));
        var process = _launcher.Start(program, Arguments(_store.FileOf(model), model), key, line => LlamaServerOutput.ReadAddress(line) is not null);
        lock (_sync)
        {
            _starting = process;
            _model = model;
        }

        try
        {
            // A stop that came while the process was being started found nothing to end yet.
            if (WasStopped(stops))
            {
                throw Unloaded(model);
            }

            var ready = await WaitReadyAsync(process, model, cancellationToken).ConfigureAwait(false);
            if (ready is null || !LanguageModelEndpoint.TryCreate(LlamaServerOutput.ReadAddress(ready) + API_PATH, key, out var endpoint))
            {
                if (WasStopped(stops))
                {
                    throw Unloaded(model);
                }

                throw LocalizedError.Create(m => new InvalidDataException(m), "LlamaServer.Failed", model.Name, process.LastWords);
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
    private async Task<string?> WaitReadyAsync(IServerProcess process, TModel model, CancellationToken cancellationToken)
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
                "LlamaServer.Timeout",
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
    /// Tells whether the server was stopped by the user since a start began.
    /// </summary>
    /// <param name="stops">How many stops there were when the start began.</param>
    /// <returns><see langword="true"/> when it was stopped since.</returns>
    private bool WasStopped(int stops)
    {
        lock (_sync)
        {
            return _stops != stops;
        }
    }

    /// <summary>
    /// Builds the failure of a start the user stopped: the model was unloaded, it is not broken.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The failure.</returns>
    private static InvalidDataException Unloaded(TModel model)
    {
        return LocalizedError.Create(m => new InvalidDataException(m), "LlamaServer.Unloaded", model.Name);
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
    /// Ends the running server and forgets it; the caller holds the gate, or the user or the application stops it.
    /// </summary>
    private void End()
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
}
