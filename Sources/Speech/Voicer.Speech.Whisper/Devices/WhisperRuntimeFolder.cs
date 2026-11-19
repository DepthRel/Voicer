using System.Runtime.InteropServices;

namespace Voicer.Speech.Whisper.Devices;

/// <summary>
/// Where the native libraries of a runtime of Whisper.net lie: <c>runtimes/&lt;runtime&gt;/&lt;platform&gt;</c> next to the
/// program (<c>runtimes/&lt;platform&gt;</c> for the plain processor runtime), and how the ggml libraries are named there.
/// </summary>
public static class WhisperRuntimeFolder
{
    /// <summary>
    /// The name of the ggml library that keeps the registry of the devices, without prefix and extension.
    /// </summary>
    public const string GGML_LIBRARY = "ggml-whisper";

    /// <summary>
    /// The name of the base ggml library that describes a device, without prefix and extension.
    /// </summary>
    public const string GGML_BASE_LIBRARY = "ggml-base-whisper";

    /// <summary>
    /// Gets the folder of the libraries of a runtime.
    /// </summary>
    /// <param name="baseDirectory">The folder of the program.</param>
    /// <param name="runtime">The name of the runtime as Whisper.net names it (<c>Vulkan</c>, <c>Cpu</c>, …).</param>
    /// <returns>The full path of the folder.</returns>
    public static string Of(string baseDirectory, string runtime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtime);

        var platform = Platform();
        var folder = runtime switch
        {
            "Cpu" => Path.Combine("runtimes", platform),
            "CpuNoAvx" => Path.Combine("runtimes", "noavx", platform),
            _ => Path.Combine("runtimes", runtime.ToLowerInvariant(), platform),
        };

        return Path.Combine(Path.GetFullPath(baseDirectory), folder);
    }

    /// <summary>
    /// Gets the file name of a native library on this system.
    /// </summary>
    /// <param name="name">The name without prefix and extension.</param>
    /// <returns>The file name, such as <c>ggml-whisper.dll</c> or <c>libggml-whisper.so</c>.</returns>
    public static string FileName(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            return name + ".dll";
        }

        return OperatingSystem.IsMacOS() ? $"lib{name}.dylib" : $"lib{name}.so";
    }

    /// <summary>
    /// Gets the name Whisper.net gives the platform of this process, such as <c>win-x64</c> or <c>macos-arm64</c>.
    /// </summary>
    /// <returns>The name of the platform.</returns>
    private static string Platform()
    {
        var system = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "macos" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            var other => other.ToString().ToLowerInvariant(),
        };

        return $"{system}-{architecture}";
    }
}
