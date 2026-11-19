namespace Voicer.Speech.Whisper.Enums;

/// <summary>
/// The type of a ggml backend device, as <c>enum ggml_backend_dev_type</c> of ggml numbers it.
/// </summary>
internal enum GgmlDeviceType
{
    /// <summary>
    /// The processor, with the memory of the computer.
    /// </summary>
    Cpu = 0,

    /// <summary>
    /// A video card with memory of its own.
    /// </summary>
    Gpu = 1,

    /// <summary>
    /// A video card that shares the memory of the computer.
    /// </summary>
    IntegratedGpu = 2,

    /// <summary>
    /// An accelerator used together with the processor (BLAS and the like); it cannot be chosen on its own.
    /// </summary>
    Accelerator = 3,
}
