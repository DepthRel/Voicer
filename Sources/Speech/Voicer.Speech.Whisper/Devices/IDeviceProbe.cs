using Voicer.Core.Devices;

namespace Voicer.Speech.Whisper.Devices;

/// <summary>
/// Lists the compute devices the speech engine can run a model on.
/// </summary>
public interface IDeviceProbe
{
    /// <summary>
    /// Lists the devices: the processor and the video cards, in the order the engine numbers them.
    /// </summary>
    /// <returns>The devices.</returns>
    /// <exception cref="InvalidOperationException">The native library of the engine cannot be used.</exception>
    IReadOnlyList<ComputeDevice> Probe();
}
