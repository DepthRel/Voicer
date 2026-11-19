using CommunityToolkit.Mvvm.ComponentModel;
using Voicer.Core.Models;
using Voicer.Downloads;
using Voicer.Downloads.Models;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A speech recognition model family in the window of models: the quantization chosen for the download, whether it is
/// on this computer, and its download.
/// </summary>
public sealed partial class ModelItemViewModel : ModelCardViewModel
{
    /// <summary>
    /// The store that knows the installed models.
    /// </summary>
    private readonly IModelStore _store;

    /// <summary>
    /// The installer that downloads the model.
    /// </summary>
    private readonly IModelInstaller _installer;

    /// <summary>
    /// The quantization chosen for the download.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Model), nameof(SizeText), nameof(QuantizationDescription), nameof(RunsOnCpu))]
    private ModelVariantViewModel _selectedVariant;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelItemViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="variants">
    /// The files of one model family, from the most accurate quantization to the smallest; at least one.
    /// </param>
    /// <param name="store">The store that knows the installed models.</param>
    /// <param name="installer">The installer that downloads the model.</param>
    /// <param name="dialogs">The dialogs that open the folder of the model.</param>
    public ModelItemViewModel(
        ILocalizer localizer,
        IReadOnlyList<SpeechModel> variants,
        IModelStore store,
        IModelInstaller installer,
        IDialogService dialogs)
        : base(localizer, dialogs)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (variants.Count == 0 || variants.Any(v => v.Family != variants[0].Family))
        {
            throw new ArgumentException("The variants must be one or more files of the same model family.", nameof(variants));
        }

        _store = store ?? throw new ArgumentNullException(nameof(store));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));

        Variants = [.. variants.Select(v => new ModelVariantViewModel(localizer, v, store.IsInstalled(v)))];

        // A quantization that is already here is shown first; otherwise the most accurate one is offered.
        _selectedVariant = Variants.FirstOrDefault(v => v.IsInstalled) ?? Variants[0];
        IsInstalled = _selectedVariant.IsInstalled;
    }

    /// <summary>
    /// Gets the quantizations of the model family, from the most accurate to the smallest.
    /// </summary>
    public IReadOnlyList<ModelVariantViewModel> Variants { get; }

    /// <summary>
    /// Gets the file of the model in the chosen quantization.
    /// </summary>
    public SpeechModel Model => SelectedVariant.Model;

    /// <inheritdoc />
    public override string Name => Model.Name;

    /// <inheritdoc />
    public override string Description => Localizer.Get(Model.DescriptionKey);

    /// <summary>
    /// Gets what the chosen quantization gives and what it costs.
    /// </summary>
    public string QuantizationDescription => SelectedVariant.Description;

    /// <inheritdoc />
    public override string SizeText => SelectedVariant.SizeText;

    /// <inheritdoc />
    public override string Folder => _store.DirectoryOf(Model);

    /// <inheritdoc />
    public override bool RunsOnCpu => Model.RunsOnCpu;

    /// <inheritdoc />
    protected override Task InstallAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        return _installer.InstallAsync(SelectedVariant.Model, progress, cancellationToken);
    }

    /// <inheritdoc />
    protected override bool CheckInstalled()
    {
        SelectedVariant.IsInstalled = _store.IsInstalled(SelectedVariant.Model);

        return SelectedVariant.IsInstalled;
    }

    /// <inheritdoc />
    protected override long ReceivedBytes()
    {
        var part = new FileInfo(_store.FileOf(Model) + FileDownloader.PART_EXTENSION);

        return part.Exists ? part.Length : 0;
    }

    /// <summary>
    /// Shows the state of the newly chosen quantization and forgets the failure of the previous one.
    /// </summary>
    /// <param name="value">The chosen quantization.</param>
    partial void OnSelectedVariantChanged(ModelVariantViewModel value)
    {
        RefreshState();
    }
}
