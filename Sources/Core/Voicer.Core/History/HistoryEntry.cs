using Voicer.Core.Enums;

namespace Voicer.Core.History;

/// <summary>
/// A recognition or a generation in the list of the history: what is shown about it without reading its whole text.
/// </summary>
/// <param name="Id">The identifier of the record in the history.</param>
/// <param name="SourceName">The name of the recognized or generated file, without its folder.</param>
/// <param name="ModelName">The model that recognized or spoke it.</param>
/// <param name="DeviceName">The device it was recognized on.</param>
/// <param name="Language">The detected language of the speech, or <see langword="null"/> when it is not known.</param>
/// <param name="CreatedAt">When the recognition finished.</param>
/// <param name="Duration">How long the sound lasts; without a kept sound, the end of the last fragment of the speech.</param>
/// <param name="Preview">The beginning of the text.</param>
/// <param name="HasAudio">Whether the sound of the file is kept with the text.</param>
/// <param name="Kind">Whether the speech was recognized or generated.</param>
public sealed record HistoryEntry(
    long Id,
    string SourceName,
    string ModelName,
    string DeviceName,
    string? Language,
    DateTimeOffset CreatedAt,
    TimeSpan Duration,
    string Preview,
    bool HasAudio,
    RecordKind Kind = RecordKind.Transcription);
