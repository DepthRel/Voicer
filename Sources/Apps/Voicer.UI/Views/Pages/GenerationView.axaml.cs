using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Views.Pages;

/// <summary>
/// The Generation tab: the model, the voice and the text, the generation and the player of its result.
/// </summary>
public partial class GenerationView : UserControl
{
    /// <summary>
    /// How often the player is followed on the screen.
    /// </summary>
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Follows the player while the page is shown.
    /// </summary>
    private readonly DispatcherTimer _refresh;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationView"/> class.
    /// </summary>
    public GenerationView()
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
    /// Shows where the player is.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRefresh(object? sender, EventArgs e)
    {
        (DataContext as GenerationViewModel)?.Player?.Refresh();
    }
}
