using Voicer.Core.Enums;

namespace Voicer.UI.Services.Worker;

/// <summary>
/// The worker process failed: it named the failure, or it ended without a word (a native crash).
/// </summary>
public sealed class WorkerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerException"/> class.
    /// </summary>
    public WorkerException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerException"/> class.
    /// </summary>
    /// <param name="message">The message, in English.</param>
    public WorkerException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerException"/> class.
    /// </summary>
    /// <param name="message">The message, in English.</param>
    /// <param name="innerException">The cause.</param>
    public WorkerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerException"/> class.
    /// </summary>
    /// <param name="message">The message, in English.</param>
    /// <param name="code">The failure the worker named, or <see langword="null"/> when it ended without a word.</param>
    /// <param name="exitCode">The exit code of the worker.</param>
    public WorkerException(string message, WorkerErrorCode? code, int exitCode)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    /// <summary>
    /// Gets the failure the worker named, or <see langword="null"/> when it ended without a word.
    /// </summary>
    public WorkerErrorCode? Code { get; }

    /// <summary>
    /// Gets the exit code of the worker.
    /// </summary>
    public int ExitCode { get; }
}
