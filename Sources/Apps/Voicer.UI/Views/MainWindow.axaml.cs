using Avalonia.Controls;
using Avalonia.Threading;
using Voicer.UI.ViewModels;

namespace Voicer.UI.Views;

/// <summary>
/// The main window: the tab strip with the models of Voicer held in memory, and the page of the selected tab.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// How often the models in memory are read again.
    /// </summary>
    private static readonly TimeSpan _modelsInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Reads the models in memory again while the window is open.
    /// </summary>
    private readonly DispatcherTimer _models;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _models = new DispatcherTimer(_modelsInterval, DispatcherPriority.Background, OnModelsTick);
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _models.Start();
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _models.Stop();
        base.OnClosed(e);
    }

    /// <summary>
    /// Reads the models in memory again.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnModelsTick(object? sender, EventArgs e)
    {
        (DataContext as MainViewModel)?.RefreshLoadedModels();
    }
}
