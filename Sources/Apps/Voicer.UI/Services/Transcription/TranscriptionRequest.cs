using Voicer.Core.Devices;
using Voicer.Core.Models;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// What to recognize, with which model and where.
/// </summary>
/// <param name="SourcePath">The audio or video file.</param>
/// <param name="Model">The model.</param>
/// <param name="ModelPath">The file of the model.</param>
/// <param name="Device">The device the model runs on.</param>
/// <param name="DeviceName">The name of the device as it is shown to the user.</param>
/// <param name="DetectorPath">The file of the detector of speech, or <see langword="null"/> to recognize the whole sound.</param>
public sealed record TranscriptionRequest(
    string SourcePath,
    SpeechModel Model,
    string ModelPath,
    ComputeDevice Device,
    string DeviceName,
    string? DetectorPath = null);
