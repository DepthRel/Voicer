using Avalonia.Controls;

namespace Voicer.UI.Views.Controls;

/// <summary>
/// A model of Voicer in the settings: its catalog, and the state and the stop of the llama-server that runs it.
/// </summary>
public partial class LocalModelView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalModelView"/> class.
    /// </summary>
    public LocalModelView()
    {
        InitializeComponent();
    }
}
