using System.Text.Json.Serialization;
using Voicer.Core.Enums;

namespace Voicer.Core.Devices;

/// <summary>
/// A device a speech model can run on: the processor or a video card, with its memory.
/// </summary>
/// <param name="Kind">What the device is.</param>
/// <param name="Index">
/// The number of a video card among the video cards the speech engine sees, in its order; <c>-1</c> for the processor.
/// </param>
/// <param name="Name">The name of the device as its driver gives it.</param>
/// <param name="FreeBytes">The memory free for a model, in bytes; <c>0</c> when unknown.</param>
/// <param name="TotalBytes">The whole memory of the device, in bytes; <c>0</c> when unknown.</param>
public sealed record ComputeDevice(ComputeDeviceKind Kind, int Index, string Name, long FreeBytes, long TotalBytes)
{
    /// <summary>
    /// The index of the processor, which is not a video card.
    /// </summary>
    public const int CPU_INDEX = -1;

    /// <summary>
    /// Gets a value indicating whether the device is a video card.
    /// </summary>
    [JsonIgnore]
    public bool IsGpu => Kind != ComputeDeviceKind.Cpu;
}
