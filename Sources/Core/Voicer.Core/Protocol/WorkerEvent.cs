using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Core.Transcripts;

namespace Voicer.Core.Protocol;

/// <summary>
/// A line the worker process writes to its standard output: what it found, how far it got, what it recognized, or why
/// it failed. Only the members of the kind are set.
/// </summary>
/// <param name="Kind">What the line reports.</param>
public sealed record WorkerEvent(WorkerEventKind Kind)
{
    /// <summary>
    /// Gets the compute devices, for <see cref="WorkerEventKind.Devices"/>.
    /// </summary>
    public IReadOnlyList<ComputeDevice>? Devices { get; init; }

    /// <summary>
    /// Gets how much of the audio is recognized, from 0 to 100, for <see cref="WorkerEventKind.Progress"/>.
    /// </summary>
    public double? Percent { get; init; }

    /// <summary>
    /// Gets the recognized piece of speech, for <see cref="WorkerEventKind.Segment"/>.
    /// </summary>
    public TranscriptSegment? Segment { get; init; }

    /// <summary>
    /// Gets the language the model detected, for <see cref="WorkerEventKind.Done"/>; <see langword="null"/> when unknown.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets why the work failed, for <see cref="WorkerEventKind.Error"/>.
    /// </summary>
    public WorkerErrorCode? Error { get; init; }

    /// <summary>
    /// Gets the technical detail of a failure, in English, for <see cref="WorkerEventKind.Error"/>.
    /// </summary>
    public string? Detail { get; init; }
}
