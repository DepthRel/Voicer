using System.Diagnostics;
using Voicer.Localization;

namespace Voicer.Media.Ffmpeg;

/// <summary>
/// Runs FFmpeg to turn the sound of one file into another file: checks the input, reports the progress, keeps the end
/// of the log for the failure and removes a half-written output.
/// </summary>
internal static class FfmpegConversion
{
    /// <summary>
    /// How many last lines of the log of FFmpeg are kept to explain a failure.
    /// </summary>
    private const int KEPT_LOG_LINES = 12;

    /// <summary>
    /// The options every conversion starts with: no questions, no banner, the progress as key=value lines on the
    /// standard output.
    /// </summary>
    private static readonly string[] _commonOptions = ["-nostdin", "-hide_banner", "-nostats", "-y", "-progress", "pipe:1"];

    /// <summary>
    /// Converts the sound of a file.
    /// </summary>
    /// <param name="locator">Finds FFmpeg.</param>
    /// <param name="source">The file whose sound is read.</param>
    /// <param name="destination">The file to write.</param>
    /// <param name="outputOptions">The options of the output (streams, codec, format), without the files.</param>
    /// <param name="progress">Receives the progress, from 0 to 100.</param>
    /// <param name="cancellationToken">Stops FFmpeg and removes the output.</param>
    /// <returns>How long the sound of the source lasts.</returns>
    /// <exception cref="FileNotFoundException">FFmpeg or the source is not found.</exception>
    /// <exception cref="InvalidDataException">The source has no sound, or FFmpeg failed.</exception>
    internal static async Task<TimeSpan> RunAsync(
        IFfmpegLocator locator,
        string source,
        string destination,
        IReadOnlyList<string> outputOptions,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(outputOptions);

        var ffmpeg = locator.Locate() ??
            throw LocalizedError.Create(m => new FileNotFoundException(m), "Ffmpeg.Missing");
        var input = Path.GetFullPath(source);
        if (!File.Exists(input))
        {
            throw LocalizedError.Create(m => new FileNotFoundException(m, input), "Audio.SourceMissing", input);
        }

        var output = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        using var process = new Process { StartInfo = CreateStartInfo(ffmpeg, input, output, outputOptions) };
        var log = new Queue<string>();
        var duration = TimeSpan.Zero;
        var noAudio = false;

        process.Start();

        // The log goes to the standard error, the progress to the standard output; both are read at once.
        var logTask = Task.Run(
            async () =>
            {
                while (await process.StandardError.ReadLineAsync(CancellationToken.None).ConfigureAwait(false) is { } line)
                {
                    if (duration == TimeSpan.Zero && FfmpegOutputParser.TryParseDuration(line, out var parsed))
                    {
                        duration = parsed;
                    }

                    noAudio |= FfmpegOutputParser.IsNoAudio(line);
                    log.Enqueue(line);
                    if (log.Count > KEPT_LOG_LINES)
                    {
                        log.Dequeue();
                    }
                }
            },
            CancellationToken.None);

        var position = TimeSpan.Zero;
        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (FfmpegOutputParser.TryParsePosition(line, out var parsed))
                {
                    position = parsed;
                    if (duration > TimeSpan.Zero)
                    {
                        progress?.Report(Math.Clamp(position / duration * 100, 0, 100));
                    }
                }
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await logTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            TryDelete(output);

            throw;
        }

        if (process.ExitCode != 0)
        {
            TryDelete(output);
            if (noAudio)
            {
                throw LocalizedError.Create(m => new InvalidDataException(m), "Audio.NoSoundTrack", Path.GetFileName(input));
            }

            throw LocalizedError.Create(m => new InvalidDataException(m), "Audio.ExtractFailed", process.ExitCode, log.LastOrDefault() ?? string.Empty);
        }

        progress?.Report(100);

        return duration > TimeSpan.Zero ? duration : position;
    }

    /// <summary>
    /// Ends FFmpeg and the processes it started.
    /// </summary>
    /// <param name="process">The process of FFmpeg.</param>
    internal static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process ended on its own meanwhile.
        }
    }

    /// <summary>
    /// Describes the start of FFmpeg: no shell, every argument on its own.
    /// </summary>
    /// <param name="ffmpeg">The program.</param>
    /// <param name="input">The full path of the source.</param>
    /// <param name="output">The full path of the output.</param>
    /// <param name="outputOptions">The options of the output.</param>
    /// <returns>The start information.</returns>
    private static ProcessStartInfo CreateStartInfo(string ffmpeg, string input, string output, IReadOnlyList<string> outputOptions)
    {
        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };

        // The paths are full, so a file name that starts with a dash is never read as an option.
        foreach (var argument in _commonOptions.Concat(["-i", input]).Concat(outputOptions).Append(output))
        {
            start.ArgumentList.Add(argument);
        }

        return start;
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
            // A file that stays behind is removed with the temporary folder on the next start.
        }
    }
}
