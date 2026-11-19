namespace Voicer.Core.Enums;

/// <summary>
/// What a compute device that can run a speech model is.
/// </summary>
public enum ComputeDeviceKind
{
    /// <summary>
    /// The central processor, with the memory of the computer.
    /// </summary>
    Cpu,

    /// <summary>
    /// A discrete video card with memory of its own.
    /// </summary>
    Gpu,

    /// <summary>
    /// A video card built into the processor, sharing the memory of the computer.
    /// </summary>
    IntegratedGpu,
}
