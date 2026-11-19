using Voicer.Core.Models;
using Voicer.Downloads.Models;

namespace Voicer.UI.Services.Transcription;

/// <summary>
/// <see cref="ISpeechDetectorSource"/> over the models folder: <see cref="SpeechDetectorModel.SileroVad"/>, downloaded
/// with its pin when it is missing.
/// </summary>
public sealed class SpeechDetectorSource : ISpeechDetectorSource
{
    /// <summary>
    /// The store that knows where the detector is kept.
    /// </summary>
    private readonly IModelStore _store;

    /// <summary>
    /// The installer that downloads the detector.
    /// </summary>
    private readonly IModelInstaller _installer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechDetectorSource"/> class.
    /// </summary>
    /// <param name="store">The store that knows where the detector is kept.</param>
    /// <param name="installer">The installer that downloads the detector.</param>
    public SpeechDetectorSource(IModelStore store, IModelInstaller installer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
    }

    /// <inheritdoc />
    public bool IsReady => _store.IsDetectorInstalled(SpeechDetectorModel.SileroVad);

    /// <inheritdoc />
    public async Task<string> PrepareAsync(CancellationToken cancellationToken)
    {
        if (!IsReady)
        {
            await _installer.InstallDetectorAsync(null, cancellationToken).ConfigureAwait(false);
        }

        return _store.DetectorFileOf(SpeechDetectorModel.SileroVad);
    }
}
