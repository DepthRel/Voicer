using System.ComponentModel;
using System.Diagnostics;
using Voicer.Speech.Llama.Engine;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// <see cref="IServerProcess"/> over a <see cref="Process"/>: both of its outputs are read for as long as it runs, so
/// neither pipe fills and stops it, and the last lines are kept to explain a failure.
/// </summary>
public sealed class ServerProcess : IServerProcess
{
    /// <summary>
    /// How many last lines of the output are kept.
    /// </summary>
    private const int KEPT_LINES = 20;

    /// <summary>
    /// How long the end of a stopped process is waited for.
    /// </summary>
    private const int EXIT_WAIT_MILLISECONDS = 5000;

    /// <summary>
    /// The process.
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// Tells whether a line says the server is ready.
    /// </summary>
    private readonly Func<string, bool> _isReady;

    /// <summary>
    /// Completes when the server is ready or has ended.
    /// </summary>
    private readonly TaskCompletionSource<string?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The last lines of the output.
    /// </summary>
    private readonly Queue<string> _log = new();

    /// <summary>
    /// Called once the process is ended and released, or <see langword="null"/>.
    /// </summary>
    private readonly Action? _ended;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerProcess"/> class and starts reading the outputs of the
    /// started process.
    /// </summary>
    /// <param name="process">The started process, with both outputs redirected.</param>
    /// <param name="isReady">Tells whether a line says the server is ready.</param>
    /// <param name="ended">Called once the process is ended and released (its note is forgotten), or <see langword="null"/>.</param>
    public ServerProcess(Process process, Func<string, bool> isReady, Action? ended = null)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _isReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
        _ended = ended;

        var readers = new[] { Collect(process.StandardOutput), Collect(process.StandardError) };
        _ = Task.WhenAll(readers).ContinueWith(_ => _ready.TrySetResult(null), TaskScheduler.Default);
    }

    /// <inheritdoc />
    public Task<string?> Ready => _ready.Task;

    /// <inheritdoc />
    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    /// <inheritdoc />
    public string LastWords
    {
        get
        {
            lock (_log)
            {
                return LlamaTtsSynthesizer.LastWords(_log);
            }
        }
    }

    /// <inheritdoc />
    public ServerUsage? ReadUsage()
    {
        try
        {
            // The memory is read once and kept by the process object until it is refreshed.
            _process.Refresh();
            if (_process.HasExited)
            {
                return null;
            }

            return new ServerUsage(_process.Id, _process.WorkingSet64, _process.TotalProcessorTime);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process ended meanwhile, or the system does not tell.
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(EXIT_WAIT_MILLISECONDS);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // The process ended on its own meanwhile.
        }

        _process.Dispose();
        _ended?.Invoke();
    }

    /// <summary>
    /// Reads an output to its end, keeping its last lines and watching for the line that says the server is ready.
    /// </summary>
    /// <param name="reader">The output.</param>
    /// <returns>A task that completes when the output ends.</returns>
    private Task Collect(StreamReader reader)
    {
        return Task.Run(async () =>
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                lock (_log)
                {
                    _log.Enqueue(line);
                    if (_log.Count > KEPT_LINES)
                    {
                        _log.Dequeue();
                    }
                }

                if (!_ready.Task.IsCompleted && _isReady(line))
                {
                    _ready.TrySetResult(line);
                }
            }
        });
    }
}
