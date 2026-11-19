using System.Reflection;
using System.Runtime.InteropServices;

namespace Voicer.Audio.OpenAl;

/// <summary>
/// The functions of OpenAL Soft that the output uses. The library comes from the native package of the application
/// (<c>soft_oal.dll</c> on Windows, <c>libopenal</c> elsewhere); the library of the system is the fallback.
/// </summary>
internal static partial class OpenAlNative
{
    /// <summary>
    /// The name the functions are imported from; the resolver maps it to the file of the platform.
    /// </summary>
    internal const string LIBRARY = "openal";

    /// <summary>
    /// Mono, signed 16-bit samples.
    /// </summary>
    internal const int AL_FORMAT_MONO16 = 0x1101;

    /// <summary>
    /// The buffer attached to a source.
    /// </summary>
    internal const int AL_BUFFER = 0x1009;

    /// <summary>
    /// The volume of a source.
    /// </summary>
    internal const int AL_GAIN = 0x100A;

    /// <summary>
    /// The state of a source.
    /// </summary>
    internal const int AL_SOURCE_STATE = 0x1010;

    /// <summary>
    /// The state of a source that plays.
    /// </summary>
    internal const int AL_PLAYING = 0x1012;

    /// <summary>
    /// The state of a source that stopped: every buffer in its queue counts as played.
    /// </summary>
    internal const int AL_STOPPED = 0x1014;

    /// <summary>
    /// How many buffers a source has played and not given back yet.
    /// </summary>
    internal const int AL_BUFFERS_PROCESSED = 0x1016;

    /// <summary>
    /// The sample a source plays now, counted from the first buffer it holds.
    /// </summary>
    internal const int AL_SAMPLE_OFFSET = 0x1025;

    /// <summary>
    /// No error.
    /// </summary>
    internal const int AL_NO_ERROR = 0;

    /// <summary>
    /// Initializes static members of the <see cref="OpenAlNative"/> class: the resolver of the library is installed
    /// before the first function is called.
    /// </summary>
    static OpenAlNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(OpenAlNative).Assembly, Resolve);
    }

    /// <summary>
    /// Opens a sound device.
    /// </summary>
    /// <param name="name">The name of the device; zero for the default one.</param>
    /// <returns>The device, or zero.</returns>
    [LibraryImport(LIBRARY, EntryPoint = "alcOpenDevice")]
    internal static partial IntPtr OpenDevice(IntPtr name);

    /// <summary>
    /// Closes a sound device.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <returns>Non-zero when it is closed.</returns>
    [LibraryImport(LIBRARY, EntryPoint = "alcCloseDevice")]
    internal static partial byte CloseDevice(IntPtr device);

    /// <summary>
    /// Creates a context on a device.
    /// </summary>
    /// <param name="device">The device.</param>
    /// <param name="attributes">The attributes; zero for the defaults.</param>
    /// <returns>The context, or zero.</returns>
    [LibraryImport(LIBRARY, EntryPoint = "alcCreateContext")]
    internal static partial IntPtr CreateContext(IntPtr device, IntPtr attributes);

    /// <summary>
    /// Makes a context the current one of the process.
    /// </summary>
    /// <param name="context">The context; zero for none.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(LIBRARY, EntryPoint = "alcMakeContextCurrent")]
    internal static partial byte MakeContextCurrent(IntPtr context);

    /// <summary>
    /// Destroys a context.
    /// </summary>
    /// <param name="context">The context.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alcDestroyContext")]
    internal static partial void DestroyContext(IntPtr context);

    /// <summary>
    /// Takes and clears the last error.
    /// </summary>
    /// <returns>The error, or <see cref="AL_NO_ERROR"/>.</returns>
    [LibraryImport(LIBRARY, EntryPoint = "alGetError")]
    internal static partial int GetError();

    /// <summary>
    /// Creates sources.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="sources">Receives their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alGenSources")]
    internal static partial void GenSources(int count, out uint sources);

    /// <summary>
    /// Deletes sources.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="sources">Their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alDeleteSources")]
    internal static partial void DeleteSources(int count, in uint sources);

    /// <summary>
    /// Creates buffers.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="buffers">Receives their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alGenBuffers")]
    internal static partial void GenBuffers(int count, [Out] uint[] buffers);

    /// <summary>
    /// Deletes buffers.
    /// </summary>
    /// <param name="count">How many.</param>
    /// <param name="buffers">Their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alDeleteBuffers")]
    internal static partial void DeleteBuffers(int count, [In] uint[] buffers);

    /// <summary>
    /// Fills a buffer with samples.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="format">The format of the samples.</param>
    /// <param name="data">The first byte of the samples.</param>
    /// <param name="size">The size of the samples in bytes.</param>
    /// <param name="frequency">The sample rate.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alBufferData")]
    internal static unsafe partial void BufferData(uint buffer, int format, byte* data, int size, int frequency);

    /// <summary>
    /// Queues buffers on a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="count">How many.</param>
    /// <param name="buffers">Their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourceQueueBuffers")]
    internal static partial void SourceQueueBuffers(uint source, int count, in uint buffers);

    /// <summary>
    /// Takes played buffers back from a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="count">How many.</param>
    /// <param name="buffers">Receives their names.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourceUnqueueBuffers")]
    internal static partial void SourceUnqueueBuffers(uint source, int count, out uint buffers);

    /// <summary>
    /// Plays a source.
    /// </summary>
    /// <param name="source">The source.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourcePlay")]
    internal static partial void SourcePlay(uint source);

    /// <summary>
    /// Stops a source; its queued buffers count as played.
    /// </summary>
    /// <param name="source">The source.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourceStop")]
    internal static partial void SourceStop(uint source);

    /// <summary>
    /// Puts a source back into its initial state, where queued buffers wait to be played instead of counting as played.
    /// </summary>
    /// <param name="source">The source.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourceRewind")]
    internal static partial void SourceRewind(uint source);

    /// <summary>
    /// Reads an integer property of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="parameter">The property.</param>
    /// <param name="value">Receives the value.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alGetSourcei")]
    internal static partial void GetSourcei(uint source, int parameter, out int value);

    /// <summary>
    /// Sets an integer property of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="parameter">The property.</param>
    /// <param name="value">The value.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourcei")]
    internal static partial void Sourcei(uint source, int parameter, int value);

    /// <summary>
    /// Sets a number property of a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="parameter">The property.</param>
    /// <param name="value">The value.</param>
    [LibraryImport(LIBRARY, EntryPoint = "alSourcef")]
    internal static partial void Sourcef(uint source, int parameter, float value);

    /// <summary>
    /// Finds the OpenAL library: the one of the package first, then the one of the system.
    /// </summary>
    /// <param name="name">The name the functions are imported from.</param>
    /// <param name="assembly">The assembly that imports them.</param>
    /// <param name="searchPath">Where the import allows looking.</param>
    /// <returns>The loaded library, or zero to let the runtime look on its own.</returns>
    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != LIBRARY)
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in Candidates())
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Lists the names of the OpenAL library on this platform, the one of the package first.
    /// </summary>
    /// <returns>The names.</returns>
    private static string[] Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return ["soft_oal", "OpenAL32"];
        }

        if (OperatingSystem.IsMacOS())
        {
            return ["libopenal.dylib", "libopenal.1.dylib", "/System/Library/Frameworks/OpenAL.framework/OpenAL"];
        }

        return ["libopenal.so", "libopenal.so.1"];
    }
}
