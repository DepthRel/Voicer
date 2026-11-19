using CommunityToolkit.Mvvm.Input;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// A catalog of models: in the settings, a short line with how many are downloaded and the button that opens it; in its
/// own window, the cards of the models with their downloads.
/// </summary>
public sealed partial class ModelCatalogViewModel : LocalizedViewModel
{
    /// <summary>
    /// The dialogs that open the window of the catalog.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// The key of the title of the catalog.
    /// </summary>
    private readonly string _titleKey;

    /// <summary>
    /// The key of the explanation of the catalog.
    /// </summary>
    private readonly string _hintKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCatalogViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="titleKey">The key of the title of the catalog.</param>
    /// <param name="hintKey">The key of the explanation of the catalog.</param>
    /// <param name="models">The cards of the models, in the order they are offered.</param>
    /// <param name="dialogs">The dialogs that open the window of the catalog.</param>
    public ModelCatalogViewModel(ILocalizer localizer, string titleKey, string hintKey, IReadOnlyList<ModelCardViewModel> models, IDialogService dialogs)
        : base(localizer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(hintKey);
        _titleKey = titleKey;
        _hintKey = hintKey;
        Models = models ?? throw new ArgumentNullException(nameof(models));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        foreach (var model in models)
        {
            model.Installed += OnModelChanged;
            model.Removed += OnModelChanged;
        }
    }

    /// <summary>
    /// Raised when a model of the catalog has been downloaded and checked, or deleted.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the cards of the models.
    /// </summary>
    public IReadOnlyList<ModelCardViewModel> Models { get; }

    /// <summary>
    /// Gets the title of the catalog.
    /// </summary>
    public string Title => Localizer.Get(_titleKey);

    /// <summary>
    /// Gets what the catalog holds and where the models are kept.
    /// </summary>
    public string Hint => Localizer.Get(_hintKey);

    /// <summary>
    /// Gets how many models are downloaded, in words.
    /// </summary>
    public string Summary => Localizer.Format("Models.Summary", Models.Count(m => m.IsInstalled), Models.Count);

    /// <summary>
    /// Gets a value indicating whether at least one model is downloaded.
    /// </summary>
    public bool HasInstalled => Models.Any(m => m.IsInstalled);

    /// <summary>
    /// Opens the window of the catalog; the line of the settings follows what was downloaded there.
    /// </summary>
    /// <returns>A task that completes when the window is closed.</returns>
    [RelayCommand]
    private async Task OpenAsync()
    {
        await _dialogs.ShowModelsAsync(this);
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasInstalled));
    }

    /// <summary>
    /// Follows a model that was downloaded or deleted.
    /// </summary>
    /// <param name="sender">The card of the model.</param>
    /// <param name="e">The event arguments.</param>
    private void OnModelChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasInstalled));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
