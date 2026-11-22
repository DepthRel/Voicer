namespace Voicer.Speech.Llama.Servers;

/// <summary>
/// What a running llama-server takes from the computer at a moment.
/// </summary>
/// <param name="ProcessId">The identifier of its process, as the task manager shows it.</param>
/// <param name="MemoryBytes">Its working set: the memory of the computer it holds (a model on the video card is not in it).</param>
/// <param name="ProcessorTime">How much processor time it used since it started, on all cores together.</param>
public sealed record ServerUsage(int ProcessId, long MemoryBytes, TimeSpan ProcessorTime);
