using System.Runtime.InteropServices;
using Whisper.net;
using Whisper.net.LibraryLoader;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Localization;
using Voicer.Speech.Whisper.Enums;

namespace Voicer.Speech.Whisper.Devices;

/// <summary>
/// <see cref="IDeviceProbe"/> that asks the ggml library of the runtime Whisper.net loaded for its devices. whisper.cpp
/// numbers the video cards (discrete and integrated) in the order of this registry, so the index of a device here is the
/// one the engine takes.
/// </summary>
public sealed unsafe class GgmlDeviceProbe : IDeviceProbe
{
    /// <summary>
    /// The folder of the program, where the runtimes of Whisper.net lie.
    /// </summary>
    private readonly string _baseDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GgmlDeviceProbe"/> class.
    /// </summary>
    /// <param name="baseDirectory">The folder of the program; the folder of the running application by default.</param>
    public GgmlDeviceProbe(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    /// <inheritdoc />
    public IReadOnlyList<ComputeDevice> Probe()
    {
        // Loads the first runtime that works on this computer (a video card runtime before the processor one).
        WhisperFactory.GetRuntimeInfo();
        var runtime = RuntimeOptions.LoadedLibrary?.ToString() ??
            throw LocalizedError.Create(m => new InvalidOperationException(m), "Devices.NoRuntime");

        var folder = WhisperRuntimeFolder.Of(_baseDirectory, runtime);
        var registry = NativeLibrary.Load(Path.Combine(folder, WhisperRuntimeFolder.FileName(WhisperRuntimeFolder.GGML_LIBRARY)));
        var core = NativeLibrary.Load(Path.Combine(folder, WhisperRuntimeFolder.FileName(WhisperRuntimeFolder.GGML_BASE_LIBRARY)));

        var count = (delegate* unmanaged<nuint>)NativeLibrary.GetExport(registry, "ggml_backend_dev_count");
        var get = (delegate* unmanaged<nuint, nint>)NativeLibrary.GetExport(registry, "ggml_backend_dev_get");
        var describe = (delegate* unmanaged<nint, nint>)NativeLibrary.GetExport(core, "ggml_backend_dev_description");
        var type = (delegate* unmanaged<nint, int>)NativeLibrary.GetExport(core, "ggml_backend_dev_type");
        var memory = (delegate* unmanaged<nint, nuint*, nuint*, void>)NativeLibrary.GetExport(core, "ggml_backend_dev_memory");

        var devices = new List<ComputeDevice>();
        var gpuIndex = 0;
        var total = count();
        for (nuint i = 0; i < total; i++)
        {
            var device = get(i);
            var kind = (GgmlDeviceType)type(device) switch
            {
                GgmlDeviceType.Cpu => ComputeDeviceKind.Cpu,
                GgmlDeviceType.Gpu => ComputeDeviceKind.Gpu,
                GgmlDeviceType.IntegratedGpu => ComputeDeviceKind.IntegratedGpu,
                _ => (ComputeDeviceKind?)null,
            };
            if (kind is null)
            {
                continue;
            }

            nuint free;
            nuint size;
            memory(device, &free, &size);
            var name = (Marshal.PtrToStringUTF8(describe(device)) ?? string.Empty).Trim();
            var index = kind == ComputeDeviceKind.Cpu ? ComputeDevice.CPU_INDEX : gpuIndex++;
            devices.Add(new ComputeDevice(kind.Value, index, name, (long)free, (long)size));
        }

        return devices;
    }
}
