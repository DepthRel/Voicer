namespace Voicer.UI.Services.Transcription;

/// <summary>
/// A file chosen for the recognition, checked: it is there, it can be read and it is not empty.
/// </summary>
/// <param name="FullPath">The full path of the file.</param>
/// <param name="SizeBytes">The size of the file, in bytes.</param>
public sealed record SourceFile(string FullPath, long SizeBytes);
