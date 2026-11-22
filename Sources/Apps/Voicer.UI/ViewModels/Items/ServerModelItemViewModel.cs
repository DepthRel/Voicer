using Voicer.Core.Models;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A model run by llama-server in its catalog (of the search by meaning, of the summaries): its description and size,
/// its download and its deletion.
/// </summary>
/// <typeparam name="TModel">The kind of the model.</typeparam>
public sealed class ServerModelItemViewModel<TModel> : ModelCardViewModel
    where TModel : class, IServerModel
{
    /// <summary>
    /// Where the models live.
    /// </summary>
    private readonly IServerModelStore<TModel> _store;

    /// <summary>
    /// Downloads the model.
    /// </summary>
    private readonly IServerModelInstaller<TModel> _installer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerModelItemViewModel{TModel}"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="model">The model.</param>
    /// <param name="store">Where the models live.</param>
    /// <param name="installer">Downloads the model.</param>
    /// <param name="dialogs">The dialogs that confirm a deletion and open the folder.</param>
    public ServerModelItemViewModel(
        ILocalizer localizer,
        TModel model,
        IServerModelStore<TModel> store,
        IServerModelInstaller<TModel> installer,
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
    public TModel Model { get; }

    /// <inheritdoc />
    public override string Name => Model.Name;

    /// <inheritdoc />
    public override string Description => Localizer.Get(Model.DescriptionKey);

    /// <inheritdoc />
    public override string SizeText => DisplayText.Bytes(Localizer, Model.SizeBytes);

    /// <inheritdoc />
    public override string Folder => _store.DirectoryOf(Model);

    /// <summary>
    /// Gets a value indicating whether the model runs on the processor: it does, a video card only makes it faster.
    /// </summary>
    public override bool RunsOnCpu => true;

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
        var path = _store.FileOf(Model);
        var whole = new FileInfo(path);
        if (whole.Exists && whole.Length == Model.SizeBytes)
        {
            return Model.SizeBytes;
        }

        var part = new FileInfo(path + FileDownloader.PART_EXTENSION);

        return part.Exists ? part.Length : 0;
    }
}
