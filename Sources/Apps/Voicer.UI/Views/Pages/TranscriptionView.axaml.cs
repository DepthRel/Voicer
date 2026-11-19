using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Voicer.UI.ViewModels.Pages;

namespace Voicer.UI.Views.Pages;

/// <summary>
/// The page that turns speech into text. The drop area takes a file dragged from the file manager or opens the picker
/// on a click.
/// </summary>
public partial class TranscriptionView : UserControl
{
    /// <summary>
    /// The class of the drop area while a file is dragged over it.
    /// </summary>
    private const string DRAG_OVER_CLASS = "drag-over";

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptionView"/> class.
    /// </summary>
    public TranscriptionView()
    {
        InitializeComponent();
        DropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        DropZone.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>
    /// Gets the view model of the page.
    /// </summary>
    private TranscriptionViewModel? ViewModel => DataContext as TranscriptionViewModel;

    /// <summary>
    /// Lights the drop area up when a file comes over it.
    /// </summary>
    /// <param name="sender">The drop area.</param>
    /// <param name="e">The drag.</param>
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        var accepted = HasFile(e) && ViewModel?.IsIdle == true;
        DropZone.Classes.Set(DRAG_OVER_CLASS, accepted);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
    }

    /// <summary>
    /// Tells the system whether the file would be taken.
    /// </summary>
    /// <param name="sender">The drop area.</param>
    /// <param name="e">The drag.</param>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasFile(e) && ViewModel?.IsIdle == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    /// <summary>
    /// Turns the light of the drop area off.
    /// </summary>
    /// <param name="sender">The drop area.</param>
    /// <param name="e">The drag.</param>
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Set(DRAG_OVER_CLASS, false);
    }

    /// <summary>
    /// Takes the first dropped file.
    /// </summary>
    /// <param name="sender">The drop area.</param>
    /// <param name="e">The drop.</param>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Set(DRAG_OVER_CLASS, false);
        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        ViewModel?.TrySetSource(file);
    }

    /// <summary>
    /// Opens the file picker on a click on the drop area.
    /// </summary>
    /// <param name="sender">The drop area.</param>
    /// <param name="e">The click.</param>
    private void OnDropZoneReleased(object? sender, PointerReleasedEventArgs e)
    {
        // A click on the button inside the area (forget the file) is the button's own.
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Left && ViewModel?.BrowseCommand.CanExecute(null) == true)
        {
            ViewModel.BrowseCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Tells whether a drag carries files.
    /// </summary>
    /// <param name="e">The drag.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasFile(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File);
    }
}
