using Voicer.Core.Synthesis;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A voice model in the window of models: what it can do, whether it is on this computer, and its download.
/// </summary>
public sealed class VoiceModelItemViewModel : ModelCardViewModel
{
    /// <summary>
    /// The store that knows the installed models.
    /// </summary>
    private readonly IVoiceModelStore _store;

    /// <summary>
    /// The installer that downloads the model.
    /// </summary>
    private readonly IVoiceModelInstaller _installer;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModelItemViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="model">The model.</param>
    /// <param name="store">The store that knows the installed models.</param>
    /// <param name="installer">The installer that downloads the model.</param>
    /// <param name="dialogs">The dialogs that open the folder of the model.</param>
    public VoiceModelItemViewModel(
        ILocalizer localizer,
        VoiceModel model,
        IVoiceModelStore store,
        IVoiceModelInstaller installer,
        IDialogService dialogs)
        : base(localizer, dialogs)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        IsInstalled = store.IsInstalled(model);
    }

    /// <summary>
    /// Gets the model.
    /// </summary>
    public VoiceModel Model { get; }

    /// <inheritdoc />
    public override string Name => Model.Name;

    /// <inheritdoc />
    public override string Description => Localizer.Get(Model.DescriptionKey);

    /// <inheritdoc />
    public override string SizeText => DisplayText.Bytes(Localizer, Model.SizeBytes);

    /// <inheritdoc />
    public override string Folder => _store.DirectoryOf(Model);

    /// <inheritdoc />
    public override bool RunsOnCpu => Model.RunsOnCpu;

    /// <summary>
    /// Gets a value indicating whether the model speaks in the voice of a sample recording: an advantage the card shows.
    /// </summary>
    public bool CanCloneVoice => Model.CanCloneVoice;

    /// <summary>
    /// Gets the languages the model speaks, in words.
    /// </summary>
    public string LanguagesText => Model.IsMultilingual
        ? Localizer.Format("VoiceModels.Languages", Model.Languages.Count)
        : Localizer.Get("SpeechLanguage." + Model.Languages[0]);

    /// <inheritdoc />
    protected override Task InstallAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        return _installer.InstallAsync(Model, progress, cancellationToken);
    }

    /// <inheritdoc />
    protected override bool CheckInstalled()
    {
        return _store.IsInstalled(Model);
    }

    /// <inheritdoc />
    protected override void DeleteFiles()
    {
        _store.Delete(Model);
    }

    /// <inheritdoc />
    protected override long ReceivedBytes()
    {
        var received = 0L;
        foreach (var file in Model.Files)
        {
            var path = _store.FileOf(Model, file);
            var whole = new FileInfo(path);
            var part = new FileInfo(path + FileDownloader.PART_EXTENSION);
            if (whole.Exists && whole.Length == file.SizeBytes)
            {
                received += file.SizeBytes;
            }
            else if (part.Exists)
            {
                received += part.Length;
            }
        }

        return received;
    }
}
