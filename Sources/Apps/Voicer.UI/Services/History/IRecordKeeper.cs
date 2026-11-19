using Voicer.Core.Transcripts;

namespace Voicer.UI.Services.History;

/// <summary>
/// Keeps a recognized text in the history together with the compressed sound of its file.
/// </summary>
public interface IRecordKeeper
{
    /// <summary>
    /// Compresses the sound of the recognized file and keeps it with the text. A sound that cannot be compressed, or a
    /// compression the user stopped, leaves the text kept without it.
    /// </summary>
    /// <param name="transcript">The recognized text; its source is the file whose sound is kept.</param>
    /// <param name="progress">Receives how much of the sound is compressed, from 0 to 100.</param>
    /// <param name="cancellationToken">Stops the compression; the text is kept anyway.</param>
    /// <returns>The kept record.</returns>
    /// <exception cref="IOException">The history could not keep the text.</exception>
    Task<KeptRecord> KeepAsync(Transcript transcript, IProgress<double>? progress, CancellationToken cancellationToken);
}
