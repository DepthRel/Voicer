using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Views.Dialogs;

/// <summary>
/// The window of a record: the player of its sound, the recognized text and the button that exports it to Markdown.
/// </summary>
public partial class RecordWindow : Window
{
    /// <summary>
    /// How often the player is followed on the screen.
    /// </summary>
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Follows the player while the window is open.
    /// </summary>
    private readonly DispatcherTimer _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordWindow"/> class.
    /// </summary>
    public RecordWindow()
    {
        InitializeComponent();
        _refresh = new DispatcherTimer(_refreshInterval, DispatcherPriority.Background, OnRefresh);
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _refresh.Start();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _refresh.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Shows where the player is.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRefresh(object? sender, EventArgs e)
    {
        (DataContext as RecordViewModel)?.Player?.Refresh();
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
