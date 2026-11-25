using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// The job object of Windows that holds the programs of llama.cpp the application starts: the system ends every process
/// in it when the application ends, however it ends (a failure, a kill from the task manager), so no model stays in
/// memory without the application. Elsewhere nothing is done here: <see cref="ServerProcessRegistry"/> ends a server
/// left behind on the next start.
/// </summary>
internal static partial class ChildProcessJob
{
    /// <summary>
    /// The class of the information <c>SetInformationJobObject</c> takes: the extended limits.
    /// </summary>
    private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9;

    /// <summary>
    /// The limit that ends every process of the job when its last handle is closed.
    /// </summary>
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

    /// <summary>
    /// The job, created on the first use; zero when it could not be. Its handle is never closed by the application: the
    /// system closes it with the process of the application, and that ends the processes of the job.
    /// </summary>
    private static readonly Lazy<nint> _job = new(Create);

    /// <summary>
    /// Puts a started process into the job, on Windows; elsewhere does nothing. A process the system refuses to put
    /// into the job runs on all the same: the registry of the servers still ends it on the next start.
    /// </summary>
    /// <param name="process">The started process.</param>
    internal static void Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var job = _job.Value;
        if (job != 0)
        {
            AssignProcessToJobObject(job, process.Handle);
        }
    }

    /// <summary>
    /// Creates the job that ends its processes when its last handle is closed.
    /// </summary>
    /// <returns>The handle of the job, or zero when the system refused.</returns>
    private static nint Create()
    {
        var job = CreateJobObjectW(0, 0);
        if (job == 0)
        {
            return 0;
        }

        var limits = new ExtendedLimits { LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE };
        if (!SetInformationJobObject(job, JOB_OBJECT_EXTENDED_LIMIT_INFORMATION, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()))
        {
            CloseHandle(job);

            return 0;
        }

        return job;
    }

    /// <summary>
    /// Creates a job object of Windows.
    /// </summary>
    /// <param name="attributes">The security attributes; zero for a handle that no child inherits.</param>
    /// <param name="name">The name; zero for a job without one.</param>
    /// <returns>The handle of the job, or zero.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint CreateJobObjectW(nint attributes, nint name);

    /// <summary>
    /// Sets the limits of a job.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="infoClass">The class of the information.</param>
    /// <param name="info">The information.</param>
    /// <param name="length">Its size, in bytes.</param>
    /// <returns>Whether the limits were set.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(nint job, int infoClass, ref ExtendedLimits info, uint length);

    /// <summary>
    /// Puts a process into a job.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="process">The handle of the process.</param>
    /// <returns>Whether the process is in the job.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(nint job, nint process);

    /// <summary>
    /// Closes a handle.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <returns>Whether it was closed.</returns>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    /// <summary>
    /// <c>JOBOBJECT_EXTENDED_LIMIT_INFORMATION</c>: the basic limits, the counters of input and output and the limits of
    /// memory, laid out flat in the order and alignment of the structure of Windows.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        /// <summary>
        /// The limit of the user time of each process.
        /// </summary>
        public long PerProcessUserTimeLimit;

        /// <summary>
        /// The limit of the user time of the job.
        /// </summary>
        public long PerJobUserTimeLimit;

        /// <summary>
        /// Which limits are in force.
        /// </summary>
        public uint LimitFlags;

        /// <summary>
        /// The smallest working set of a process.
        /// </summary>
        public nuint MinimumWorkingSetSize;

        /// <summary>
        /// The largest working set of a process.
        /// </summary>
        public nuint MaximumWorkingSetSize;

        /// <summary>
        /// How many processes the job holds at most.
        /// </summary>
        public uint ActiveProcessLimit;

        /// <summary>
        /// The processors the processes run on.
        /// </summary>
        public nuint Affinity;

        /// <summary>
        /// The priority class of the processes.
        /// </summary>
        public uint PriorityClass;

        /// <summary>
        /// The scheduling class of the processes.
        /// </summary>
        public uint SchedulingClass;

        /// <summary>
        /// The read operations of the job.
        /// </summary>
        public ulong ReadOperationCount;

        /// <summary>
        /// The write operations of the job.
        /// </summary>
        public ulong WriteOperationCount;

        /// <summary>
        /// The other operations of the job.
        /// </summary>
        public ulong OtherOperationCount;

        /// <summary>
        /// The bytes the job read.
        /// </summary>
        public ulong ReadTransferCount;

        /// <summary>
        /// The bytes the job wrote.
        /// </summary>
        public ulong WriteTransferCount;

        /// <summary>
        /// The bytes of the other operations of the job.
        /// </summary>
        public ulong OtherTransferCount;

        /// <summary>
        /// The limit of the memory of a process.
        /// </summary>
        public nuint ProcessMemoryLimit;

        /// <summary>
        /// The limit of the memory of the job.
        /// </summary>
        public nuint JobMemoryLimit;

        /// <summary>
        /// The most memory a process used.
        /// </summary>
        public nuint PeakProcessMemoryUsed;

        /// <summary>
        /// The most memory the job used.
        /// </summary>
        public nuint PeakJobMemoryUsed;
    }
}
