using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Voicer.UI.Views.Dialogs;

/// <summary>
/// The window of a catalog of models: a card per model with its description, its advantages and its download.
/// </summary>
public partial class ModelsWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelsWindow"/> class.
    /// </summary>
    public ModelsWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
