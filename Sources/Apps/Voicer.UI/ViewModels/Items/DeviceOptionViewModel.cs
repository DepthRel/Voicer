using Voicer.Core.Devices;
using Voicer.Core.Enums;
using Voicer.Localization;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A compute device in the list of devices: its name and how much memory it has free.
/// </summary>
public sealed class DeviceOptionViewModel : LocalizedViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceOptionViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="device">The device.</param>
    public DeviceOptionViewModel(ILocalizer localizer, ComputeDevice device)
        : base(localizer)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>
    /// Gets the device.
    /// </summary>
    public ComputeDevice Device { get; }

    /// <summary>
    /// Gets the name of the device as it is shown: the processor and an integrated video card are called so.
    /// </summary>
    public string Title => Device.Kind switch
    {
        ComputeDeviceKind.Cpu when string.IsNullOrWhiteSpace(Device.Name) => Localizer.Get("Device.CpuUnnamed"),
        ComputeDeviceKind.Cpu => Localizer.Format("Device.Cpu", Device.Name),
        ComputeDeviceKind.IntegratedGpu => Localizer.Format("Device.IntegratedGpu", Device.Name),
        _ => Device.Name,
    };

    /// <summary>
    /// Gets the memory of the device: free and total memory of the video card, or of the computer for the processor.
    /// </summary>
    public string Memory
    {
        get
        {
            if (Device.TotalBytes <= 0)
            {
                return string.Empty;
            }

            var key = Device.Kind == ComputeDeviceKind.Cpu ? "Device.Ram" : "Device.Vram";
            if (Device.FreeBytes <= 0)
            {
                return Localizer.Format(key + "Total", DisplayText.Bytes(Localizer, Device.TotalBytes));
            }

            return Localizer.Format(key, DisplayText.Bytes(Localizer, Device.FreeBytes), DisplayText.Bytes(Localizer, Device.TotalBytes));
        }
    }

    /// <summary>
    /// Gets the key of the icon of the device.
    /// </summary>
    public string IconKey => Device.IsGpu ? "Icon.Gpu" : "Icon.Cpu";
}
