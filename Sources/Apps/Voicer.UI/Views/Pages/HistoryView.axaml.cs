using Avalonia.Controls;
using Avalonia.Input;
using Voicer.UI.ViewModels.Items;

namespace Voicer.UI.Views.Pages;

/// <summary>
/// The page of the history: the list of the recognized texts.
/// </summary>
public partial class HistoryView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryView"/> class.
    /// </summary>
    public HistoryView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens a record on a double click on its card.
    /// </summary>
    /// <param name="sender">The card of the record.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRecordDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: HistoryItemViewModel item } && item.OpenCommand.CanExecute(null))
        {
            item.OpenCommand.Execute(null);
        }
    }

    /// <summary>
    /// Opens the record of a found passage at its moment on a double click on its card.
    /// </summary>
    /// <param name="sender">The card of the passage.</param>
    /// <param name="e">The event arguments.</param>
    private void OnHitDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: SearchHitViewModel hit } && hit.OpenCommand.CanExecute(null))
        {
            hit.OpenCommand.Execute(null);
        }
    }
}
