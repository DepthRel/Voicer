using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Voicer.UI.Views.Dialogs;

/// <summary>
/// The window that asks to confirm an action that cannot be undone; it closes with <see langword="true"/> when the
/// action is confirmed. Cancelling is the default button, so Enter does not destroy anything by accident.
/// </summary>
public partial class ConfirmDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmDialog"/> class.
    /// </summary>
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Closes the window with the action confirmed.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>
    /// Closes the window with the action cancelled.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
