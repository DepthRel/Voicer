using CommunityToolkit.Mvvm.ComponentModel;
using Voicer.Localization;

namespace Voicer.UI.ViewModels;

/// <summary>
/// Base class of the view models that live as long as the application and word texts in code: when the user switches
/// the language, every property is announced as changed, so the texts are worded again.
/// </summary>
public abstract class LocalizedViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizedViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    protected LocalizedViewModel(ILocalizer localizer)
    {
        Localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        Localizer.LanguageChanged += (_, _) => OnLanguageChanged();
    }

    /// <summary>
    /// Gets the localizer of the texts.
    /// </summary>
    protected ILocalizer Localizer { get; }

    /// <summary>
    /// Words the texts again in the new language.
    /// </summary>
    protected virtual void OnLanguageChanged()
    {
        OnPropertyChanged(string.Empty);
    }
}
