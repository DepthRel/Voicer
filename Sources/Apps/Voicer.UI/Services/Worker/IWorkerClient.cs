using Voicer.Core.Devices;
using Voicer.Core.Protocol;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// Runs commands in the worker process, which holds the speech engine.
/// </summary>
public interface IWorkerClient
{
    /// <summary>
    /// Lists the compute devices the speech engine can use.
    /// </summary>
    /// <param name="cancellationToken">Stops the worker.</param>
    /// <returns>The devices, in the order the engine numbers them.</returns>
    /// <exception cref="WorkerException">The worker failed.</exception>
    Task<IReadOnlyList<ComputeDevice>> ListDevicesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Recognizes the speech of a WAV file.
    /// </summary>
    /// <param name="modelPath">The file of the model.</param>
    /// <param name="audioPath">The WAV file (16 kHz, mono, 16-bit).</param>
    /// <param name="deviceIndex">The index of the video card, or <c>-1</c> for the processor.</param>
    /// <param name="progress">Receives the progress events of the worker, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the worker.</param>
    /// <returns>What the worker recognized.</returns>
    /// <exception cref="WorkerException">The worker failed.</exception>
    Task<WorkerTranscription> TranscribeAsync(
        string modelPath,
        string audioPath,
        int deviceIndex,
        IProgress<WorkerEvent>? progress,
        CancellationToken cancellationToken);
}
