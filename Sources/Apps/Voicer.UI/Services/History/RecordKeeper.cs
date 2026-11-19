using System.ComponentModel;
using Voicer.Core.History;
using Voicer.Core.Transcripts;
using Voicer.Media;

namespace Voicer.UI.Services.History;

/// <summary>
/// <see cref="IRecordKeeper"/> that compresses the sound with FFmpeg into a temporary file, keeps it in the history and
/// removes the file.
/// </summary>
public sealed class RecordKeeper : IRecordKeeper
{
    /// <summary>
    /// The history of the recognitions.
    /// </summary>
    private readonly ITranscriptHistory _history;

    /// <summary>
    /// Compresses the sound.
    /// </summary>
    private readonly IAudioEncoder _encoder;

    /// <summary>
    /// The folder of the compressed sound before it goes into the history.
    /// </summary>
    private readonly TemporaryAudioFolder _temporary;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordKeeper"/> class.
    /// </summary>
    /// <param name="history">The history of the recognitions.</param>
    /// <param name="encoder">Compresses the sound.</param>
    /// <param name="temporary">The folder of the compressed sound before it goes into the history.</param>
    public RecordKeeper(ITranscriptHistory history, IAudioEncoder encoder, TemporaryAudioFolder temporary)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _temporary = temporary ?? throw new ArgumentNullException(nameof(temporary));
    }

    /// <inheritdoc />
    public async Task<KeptRecord> KeepAsync(Transcript transcript, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var file = _temporary.NewFile();
        try
        {
            RecordAudio? audio = null;
            Exception? failure = null;
            try
            {
                var duration = await _encoder.EncodeAsync(transcript.SourcePath, file, progress, cancellationToken);
                audio = new RecordAudio(file, duration);
            }
            catch (OperationCanceledException)
            {
                // Stopped by the user: the text that is already recognized is kept without its sound.
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
            {
                failure = ex;
            }

            // The text is kept even when the user stopped the compression: it is recognized already.
            var id = await _history.AddAsync(transcript, audio, CancellationToken.None);

            return new KeptRecord(id, audio is not null, failure);
        }
        finally
        {
            _temporary.Delete(file);
        }
    }
}
