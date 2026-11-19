using Voicer.Localization;

namespace Voicer.UI.ViewModels.Pages;

/// <summary>
/// Base class of the pages behind the tabs of the main window.
/// </summary>
public abstract class PageViewModelBase : LocalizedViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PageViewModelBase"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    protected PageViewModelBase(ILocalizer localizer)
        : base(localizer)
    {
    }

    /// <summary>
    /// Called every time the page is shown, so it can bring what it shows up to date.
    /// </summary>
    public virtual void Activate()
    {
    }
}
