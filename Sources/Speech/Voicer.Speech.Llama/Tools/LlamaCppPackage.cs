using System.Runtime.InteropServices;

namespace Voicer.Speech.Llama.Tools;

/// <summary>
/// The build of llama.cpp the application installs: one release of the llama.cpp project (MIT), pinned by the size and
/// SHA-256 of its archive for every system. Windows and Linux take the Vulkan build, which runs on any video card and on
/// the processor; macOS on Apple silicon takes the build with Metal.
/// </summary>
public static class LlamaCppPackage
{
    /// <summary>
    /// The release of llama.cpp: the speech of every voice model of the catalog is supported by it.
    /// </summary>
    public const string VERSION = "b12531";

    /// <summary>
    /// Gets the build for Windows on x64, with Vulkan.
    /// </summary>
    public static LlamaCppBuild WindowsX64 { get; } = new(
        $"llama-{VERSION}-bin-win-vulkan-x64.zip",
        38_907_296,
        "68bae992c8e92198282ca7f1abd1ae121bffd72d0401f45155f3cf563350bfcd");

    /// <summary>
    /// Gets the build for Linux on x64, with Vulkan.
    /// </summary>
    public static LlamaCppBuild LinuxX64 { get; } = new(
        $"llama-{VERSION}-bin-ubuntu-vulkan-x64.tar.gz",
        36_968_023,
        "38eaab84338b9c7ec6be450856e0add797526d20e37c28b5c4430cadde8f1a9b");

    /// <summary>
    /// Gets the build for macOS on Apple silicon, with Metal.
    /// </summary>
    public static LlamaCppBuild MacArm64 { get; } = new(
        $"llama-{VERSION}-bin-macos-arm64.tar.gz",
        17_335_675,
        "c93676902ba830ac41233885c094134645038a23fedc2010eef96ad6b335870c");

    /// <summary>
    /// Chooses the build of a system.
    /// </summary>
    /// <param name="platform">The operating system.</param>
    /// <param name="architecture">The architecture of the processor.</param>
    /// <returns>The build, or <see langword="null"/> when llama.cpp publishes none for the system.</returns>
    public static LlamaCppBuild? For(OSPlatform platform, Architecture architecture)
    {
        if (platform == OSPlatform.Windows && architecture == Architecture.X64)
        {
            return WindowsX64;
        }

        if (platform == OSPlatform.Linux && architecture == Architecture.X64)
        {
            return LinuxX64;
        }

        if (platform == OSPlatform.OSX && architecture == Architecture.Arm64)
        {
            return MacArm64;
        }

        return null;
    }

    /// <summary>
    /// Chooses the build of the system the application runs on.
    /// </summary>
    /// <returns>The build, or <see langword="null"/> when llama.cpp publishes none for this system.</returns>
    public static LlamaCppBuild? ForCurrentSystem()
    {
        OSPlatform? platform = OperatingSystem.IsWindows() ? OSPlatform.Windows :
            OperatingSystem.IsLinux() ? OSPlatform.Linux :
            OperatingSystem.IsMacOS() ? OSPlatform.OSX :
            null;

        return platform is { } known ? For(known, RuntimeInformation.OSArchitecture) : null;
    }
}
