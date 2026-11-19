using CommunityToolkit.Mvvm.ComponentModel;
using Voicer.Core.Models;
using Voicer.Localization;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// One quantization of a model family in the settings: what it trades, how large it is and whether it is downloaded.
/// </summary>
public sealed partial class ModelVariantViewModel : LocalizedViewModel
{
    /// <summary>
    /// Whether the file of this quantization is downloaded.
    /// </summary>
    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelVariantViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="model">The file of the model in this quantization.</param>
    /// <param name="isInstalled">Whether the file is downloaded already.</param>
    public ModelVariantViewModel(ILocalizer localizer, SpeechModel model, bool isInstalled)
        : base(localizer)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _isInstalled = isInstalled;
    }

    /// <summary>
    /// Gets the file of the model in this quantization.
    /// </summary>
    public SpeechModel Model { get; }

    /// <summary>
    /// Gets the short name of the quantization.
    /// </summary>
    public string Label => Localizer.Get($"Quantization.{Model.Quantization}.Name");

    /// <summary>
    /// Gets what the quantization gives and what it costs.
    /// </summary>
    public string Description => Localizer.Get($"Quantization.{Model.Quantization}.Description");

    /// <summary>
    /// Gets the size of the download.
    /// </summary>
    public string SizeText => DisplayText.Bytes(Localizer, Model.SizeBytes);
}
