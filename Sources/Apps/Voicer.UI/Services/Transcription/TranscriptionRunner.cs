using Voicer.Core.Enums;
using Voicer.Core.Protocol;
using Voicer.Core.Transcripts;
using Voicer.Media;
using Voicer.UI.Enums;
using Voicer.UI.Services.Worker;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// <see cref="ITranscriptionRunner"/>: FFmpeg extracts the sound into a temporary WAV file, the worker process
/// recognizes it, and the temporary file is removed whatever happens.
/// </summary>
public sealed class TranscriptionRunner : ITranscriptionRunner
{
    /// <summary>
    /// The pattern of the temporary sound files.
    /// </summary>
    private const string TEMPORARY_PATTERN = "*.wav";

    /// <summary>
    /// The extractor of the sound.
    /// </summary>
    private readonly IAudioExtractor _extractor;

    /// <summary>
    /// The worker that runs the model.
    /// </summary>
    private readonly IWorkerClient _worker;

    /// <summary>
    /// The folder of the temporary files.
    /// </summary>
    private readonly string _temporaryDirectory;

    /// <summary>
    /// The clock that dates the transcripts.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionRunner"/> class.
    /// </summary>
    /// <param name="extractor">The extractor of the sound.</param>
    /// <param name="worker">The worker that runs the model.</param>
    /// <param name="temporaryDirectory">The folder of the temporary files.</param>
    /// <param name="time">The clock that dates the transcripts; the system clock by default.</param>
    public TranscriptionRunner(IAudioExtractor extractor, IWorkerClient worker, string temporaryDirectory, TimeProvider? time = null)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
        _temporaryDirectory = Path.GetFullPath(temporaryDirectory);
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Transcript> RunAsync(TranscriptionRequest request, IProgress<TranscriptionProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var audio = Path.Combine(_temporaryDirectory, $"{Guid.NewGuid():N}.wav");
        try
        {
            progress?.Report(new TranscriptionProgress(TranscriptionStage.ExtractingAudio, 0));
            await _extractor.ExtractAsync(
                request.SourcePath,
                audio,
                new SyncProgress<double>(p => progress?.Report(new TranscriptionProgress(TranscriptionStage.ExtractingAudio, p))),
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new TranscriptionProgress(TranscriptionStage.LoadingModel, 0));
            var result = await _worker.TranscribeAsync(
                request.ModelPath,
                audio,
                request.Device.Index,
                new SyncProgress<WorkerEvent>(e => progress?.Report(ToProgress(e))),
                cancellationToken).ConfigureAwait(false);

            return new Transcript(
                request.SourcePath,
                request.Model.Title,
                request.DeviceName,
                result.Language,
                _time.GetLocalNow(),
                result.Segments);
        }
        finally
        {
            Delete(audio);
        }
    }

    /// <inheritdoc />
    public void ClearLeftovers()
    {
        if (!Directory.Exists(_temporaryDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_temporaryDirectory, TEMPORARY_PATTERN))
        {
            Delete(file);
        }
    }

    /// <summary>
    /// Translates an event of the worker into the progress of the recognition.
    /// </summary>
    /// <param name="workerEvent">The event.</param>
    /// <returns>The progress.</returns>
    private static TranscriptionProgress ToProgress(WorkerEvent workerEvent)
    {
        return workerEvent.Kind == WorkerEventKind.Progress
            ? new TranscriptionProgress(TranscriptionStage.Recognizing, workerEvent.Percent ?? 0)
            : new TranscriptionProgress(TranscriptionStage.Recognizing, 0);
    }

    /// <summary>
    /// Removes a temporary file; one that cannot be removed now is removed on the next start.
    /// </summary>
    /// <param name="path">The file.</param>
    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Removed by ClearLeftovers on the next start.
        }
    }
}
