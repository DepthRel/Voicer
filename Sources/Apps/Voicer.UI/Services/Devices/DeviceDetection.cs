using System.ComponentModel;
using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.UI.Services.Worker;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Services.Devices;

/// <summary>
/// Finds the devices a model can run on through the worker process, and the one to offer first.
/// </summary>
public static class DeviceDetection
{
    /// <summary>
    /// Asks the worker for the devices; when it cannot answer, the processor is offered alone.
    /// </summary>
    /// <param name="worker">The worker that lists the devices.</param>
    /// <param name="cancellationToken">Stops the question.</param>
    /// <returns>The devices, at least the processor.</returns>
    public static async Task<IReadOnlyList<ComputeDevice>> ListAsync(IWorkerClient worker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worker);

        try
        {
            return await worker.ListDevicesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is WorkerException or IOException or InvalidOperationException or Win32Exception)
        {
            var memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

            return [new ComputeDevice(ComputeDeviceKind.Cpu, ComputeDevice.CPU_INDEX, string.Empty, 0, memory)];
        }
    }

    /// <summary>
    /// Chooses the device to offer first: a discrete video card is the fastest, then an integrated one; the processor
    /// is the last resort.
    /// </summary>
    /// <param name="devices">The devices.</param>
    /// <returns>The device, or <see langword="null"/> without devices.</returns>
    public static DeviceOptionViewModel? Preferred(IEnumerable<DeviceOptionViewModel> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var list = devices.ToList();

        return list.FirstOrDefault(d => d.Device.Kind == ComputeDeviceKind.Gpu) ??
            list.FirstOrDefault(d => d.Device.Kind == ComputeDeviceKind.IntegratedGpu) ??
            list.FirstOrDefault();
    }
}
