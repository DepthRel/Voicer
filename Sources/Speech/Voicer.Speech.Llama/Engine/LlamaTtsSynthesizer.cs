using System.ComponentModel;
using System.Diagnostics;
using Voicer.Core.Synthesis;
using Voicer.Localization;
using Voicer.Speech.Llama.Tools;

namespace Voicer.Speech.Llama.Engine;

/// <summary>
/// <see cref="ISpeechSynthesizer"/> that runs llama-tts for every piece of text: the model is loaded, speaks the piece
/// and leaves memory with the process. A failure of the program, a native one of a video driver too, ends only it and
/// is reported with the end of its log.
/// </summary>
public sealed class LlamaTtsSynthesizer : ISpeechSynthesizer
{
    /// <summary>
    /// How many last lines of the log of llama-tts are kept to explain a failure.
    /// </summary>
    private const int KEPT_LOG_LINES = 20;

    /// <summary>
    /// The locator of llama.cpp.
    /// </summary>
    private readonly ILlamaCppLocator _locator;

    /// <summary>
    /// The store of the voice models.
    /// </summary>
    private readonly IVoiceModelStore _store;

    /// <summary>
    /// Whether the system is macOS.
    /// </summary>
    private readonly bool _isMacOS;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaTtsSynthesizer"/> class.
    /// </summary>
    /// <param name="locator">The locator of llama.cpp.</param>
    /// <param name="store">The store of the voice models.</param>
    /// <param name="isMacOS">Whether the system is macOS; the system it runs on by default.</param>
    public LlamaTtsSynthesizer(ILlamaCppLocator locator, IVoiceModelStore store, bool? isMacOS = null)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _isMacOS = isMacOS ?? OperatingSystem.IsMacOS();
    }

    /// <inheritdoc />
    public async Task SynthesizeAsync(SynthesisVoice voice, string text, string destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(voice);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var program = _locator.Locate() ??
            throw LocalizedError.Create(m => new FileNotFoundException(m), "LlamaCpp.Missing");
        if (!_store.IsInstalled(voice.Model))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m), "Generation.ModelMissing", voice.Model.Name);
        }

        if (voice.SamplePath is { } sample && !File.Exists(sample))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m, sample), "Audio.SourceMissing", sample);
        }

        var output = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.Delete(output);

        var arguments = LlamaTtsCommandLine.Build(
            voice,
            _store.FileOf(voice.Model, voice.Model.Weights),
            _store.FileOf(voice.Model, voice.Model.Projector),
            text,
            output,
            _isMacOS);

        var log = new Queue<string>();
        var exitCode = await RunAsync(program, arguments, log, cancellationToken).ConfigureAwait(false);

        var written = new FileInfo(output);
        if (exitCode != 0 || !written.Exists || written.Length == 0)
        {
            TryDelete(output);

            throw LocalizedError.Create(m => new InvalidDataException(m), "Generation.EngineFailed", exitCode, LastWords(log));
        }
    }

    /// <summary>
    /// Runs llama-tts and waits for it: no shell, every argument on its own, both outputs read at once so neither fills
    /// up.
    /// </summary>
    /// <param name="program">The full path of llama-tts.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="log">Receives the last lines of its output.</param>
    /// <param name="cancellationToken">Ends the program at once.</param>
    /// <returns>The exit code of the program.</returns>
    private static async Task<int> RunAsync(string program, IReadOnlyList<string> arguments, Queue<string> log, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            WorkingDirectory = Path.GetDirectoryName(program)!,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw LocalizedError.Create(m => new InvalidDataException(m, ex), "LlamaCpp.StartFailed", ex.Message);
        }

        var readers = new[] { Collect(process.StandardOutput, log), Collect(process.StandardError, log) };
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(readers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Stop(process);

            throw;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Reads an output of the program to its end, keeping its last lines.
    /// </summary>
    /// <param name="reader">The output.</param>
    /// <param name="log">The last lines of both outputs.</param>
    /// <returns>A task that completes when the output ends.</returns>
    private static Task Collect(StreamReader reader, Queue<string> log)
    {
        return Task.Run(async () =>
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                lock (log)
                {
                    log.Enqueue(line);
                    if (log.Count > KEPT_LOG_LINES)
                    {
                        log.Dequeue();
                    }
                }
            }
        });
    }

    /// <summary>
    /// Picks the line of the log that explains a failure best: the last one that speaks of an error, or the last one.
    /// </summary>
    /// <param name="log">The last lines of the log, when the program has ended.</param>
    /// <returns>The line, or an empty text without a log.</returns>
    internal static string LastWords(IEnumerable<string> log)
    {
        var lines = log.ToList();

        return lines.LastOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || l.Contains("failed", StringComparison.OrdinalIgnoreCase)) ??
            lines.LastOrDefault() ??
            string.Empty;
    }

    /// <summary>
    /// Ends the program and what it started.
    /// </summary>
    /// <param name="process">The process.</param>
    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // The process ended on its own meanwhile.
        }
    }

    /// <summary>
    /// Removes a file that is no longer needed, if it can.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The temporary folder of the generation is removed as a whole.
        }
    }
}
