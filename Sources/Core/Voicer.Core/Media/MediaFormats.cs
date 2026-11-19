namespace Voicer.Core.Media;

/// <summary>
/// The kinds of files whose sound the application can recognize: audio files and video files with a sound track.
/// </summary>
public static class MediaFormats
{
    /// <summary>
    /// Gets the extensions of the audio files, with the leading dot, in lower case.
    /// </summary>
    public static IReadOnlyList<string> AudioExtensions { get; } =
    [
        ".mp3", ".wav", ".flac", ".ogg", ".oga", ".opus", ".m4a", ".aac", ".wma", ".aif", ".aiff", ".amr", ".ac3",
        ".mka", ".caf", ".3ga",
    ];

    /// <summary>
    /// Gets the extensions of the video files, with the leading dot, in lower case.
    /// </summary>
    public static IReadOnlyList<string> VideoExtensions { get; } =
    [
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".flv", ".ts", ".mts", ".m2ts", ".mpg", ".mpeg",
        ".vob", ".3gp", ".3g2", ".ogv", ".asf",
    ];

    /// <summary>
    /// Gets every supported extension.
    /// </summary>
    public static IReadOnlyList<string> AllExtensions { get; } = [.. AudioExtensions, .. VideoExtensions];

    /// <summary>
    /// Tells whether the sound of a file can be recognized, by the extension of its name.
    /// </summary>
    /// <param name="path">The path or the name of the file.</param>
    /// <returns><see langword="true"/> for an audio or video file of a supported kind.</returns>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);

        return AllExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
