using Avalonia.Controls;

namespace Voicer.UI.Views.Controls;

/// <summary>
/// The player of a sound, shared by the window of a record and the Generation tab; whoever shows it refreshes its view
/// model a few times a second.
/// </summary>
public partial class PlayerView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerView"/> class.
    /// </summary>
    public PlayerView()
    {
        InitializeComponent();
    }
}
