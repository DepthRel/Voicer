using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Views.Pages;

/// <summary>
/// The settings: the language, the models, the tools and the server of language models. While the page is on the
/// screen it follows the llama-server of the search by meaning.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// How often the state of the llama-server of the search by meaning is read.
    /// </summary>
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Follows the llama-server of the search by meaning while the page is shown.
    /// </summary>
    private readonly DispatcherTimer _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsView"/> class.
    /// </summary>
    public SettingsView()
    {
        InitializeComponent();
        _refresh = new DispatcherTimer(_refreshInterval, DispatcherPriority.Background, OnRefresh);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _refresh.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _refresh.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Reads the state of the llama-server of the search by meaning again.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRefresh(object? sender, EventArgs e)
    {
        (DataContext as SettingsViewModel)?.Search.Refresh();
    }
}
