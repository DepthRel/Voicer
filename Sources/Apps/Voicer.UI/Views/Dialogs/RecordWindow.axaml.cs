using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Voicer.Core.Search;
using Voicer.UI.Controls;
using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Views.Dialogs;

/// <summary>
/// The window of a record: the player of its sound, the recognized text with the phrase being said kept in view, the
/// menu that corrects the text, and the button that exports it to Markdown.
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
    /// The view model the window listens to, or <see langword="null"/>.
    /// </summary>
    private RecordViewModel? _view;

    /// <summary>
    /// The phrase marked as said last, or <see langword="null"/>.
    /// </summary>
    private TextMark? _shownPhrase;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordWindow"/> class.
    /// </summary>
    public RecordWindow()
    {
        InitializeComponent();
        _refresh = new DispatcherTimer(_refreshInterval, DispatcherPriority.Background, OnRefresh);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_view is not null)
        {
            _view.PropertyChanged -= OnViewChanged;
        }

        _view = DataContext as RecordViewModel;
        if (_view is not null)
        {
            _view.PropertyChanged += OnViewChanged;
        }
    }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _refresh.Start();

        // The text is laid out only after the window opens.
        Dispatcher.UIThread.Post(ShowPassage, DispatcherPriority.Loaded);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        _refresh.Stop();
        if (_view is not null)
        {
            _view.PropertyChanged -= OnViewChanged;
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Shows where the player is and marks the phrase being said.
    /// </summary>
    /// <param name="sender">The timer.</param>
    /// <param name="e">The event arguments.</param>
    private void OnRefresh(object? sender, EventArgs e)
    {
        _view?.Player?.Refresh();
        _view?.FollowPlayer();
    }

    /// <summary>
    /// Keeps the phrase being said in view, and opens or closes the field of a correction.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">Which property changed.</param>
    private void OnViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_view is null)
        {
            return;
        }

        if (e.PropertyName == nameof(RecordViewModel.Phrase))
        {
            var previous = _shownPhrase;
            _shownPhrase = _view.Phrase;
            if (_view.Phrase is { } phrase && _view.Player?.IsPlaying == true)
            {
                // The marks are laid out on the next pass.
                Dispatcher.UIThread.Post(() => KeepInView(phrase, previous), DispatcherPriority.Loaded);
            }
        }
        else if (e.PropertyName == nameof(RecordViewModel.IsEditing))
        {
            ShowEditing(_view.IsEditing);
        }
    }

    /// <summary>
    /// Scrolls the text to the found passage, with a third of the view above it for what was said before.
    /// </summary>
    private void ShowPassage()
    {
        if (TextMarks.PassageBounds(TextView) is { } passage)
        {
            ScrollTo(passage);
        }
    }

    /// <summary>
    /// Scrolls the text to the phrase being said when it leaves the view; a text the user scrolled away from stays where
    /// the user put it.
    /// </summary>
    /// <param name="phrase">The phrase being said.</param>
    /// <param name="previous">The phrase said before it, or <see langword="null"/>.</param>
    private void KeepInView(TextMark phrase, TextMark? previous)
    {
        if (TextMarks.RangeBounds(TextView, phrase) is not { } bounds)
        {
            return;
        }

        var before = previous is { } said ? TextMarks.RangeBounds(TextView, said) : null;
        if (PrompterScroll.Follow(bounds, before, TextScroll.Offset.Y, TextScroll.Viewport.Height, TextScroll.Extent.Height) is { } offset)
        {
            TextScroll.Offset = TextScroll.Offset.WithY(offset);
        }
    }

    /// <summary>
    /// Scrolls the text to a place, with a third of the view above it for what was said before.
    /// </summary>
    /// <param name="bounds">The place in the text.</param>
    private void ScrollTo(Rect bounds)
    {
        TextScroll.Offset = TextScroll.Offset.WithY(PrompterScroll.Place(bounds, TextScroll.Viewport.Height, TextScroll.Extent.Height));
    }

    /// <summary>
    /// Points the menu of the text at the selection, when it is clicked inside it, or at the word under the pointer.
    /// </summary>
    /// <param name="sender">The text.</param>
    /// <param name="e">The event arguments.</param>
    private void OnTextContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_view is null)
        {
            return;
        }

        int? index = e.TryGetPosition(TextView, out var point) ? TextMarks.IndexAt(TextView, point) : null;
        var start = Math.Min(TextView.SelectionStart, TextView.SelectionEnd);
        var length = Math.Abs(TextView.SelectionEnd - TextView.SelectionStart);
        var inSelection = length > 0 && (index is null || (index >= start && index < start + length));

        if (inSelection)
        {
            _view.PointAt(index ?? start, new TextMark(start, length));
        }
        else
        {
            _view.PointAt(index ?? -1);
        }
    }

    /// <summary>
    /// Tells what was recognized at a corrected place under the pointer.
    /// </summary>
    /// <param name="sender">The text.</param>
    /// <param name="e">The event arguments.</param>
    private void OnTextPointerMoved(object? sender, PointerEventArgs e)
    {
        var index = TextMarks.IndexAt(TextView, e.GetPosition(TextView));
        var hint = index is { } at ? _view?.HintAt(at) : null;
        if (!Equals(ToolTip.GetTip(TextView), hint))
        {
            ToolTip.SetTip(TextView, hint);
        }
    }

    /// <summary>
    /// Copies the selected text.
    /// </summary>
    /// <param name="sender">The menu item.</param>
    /// <param name="e">The event arguments.</param>
    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        TextView.Copy();
    }

    /// <summary>
    /// Opens the field of a correction at the piece being corrected, or closes it.
    /// </summary>
    /// <param name="editing">Whether the field is open.</param>
    private void ShowEditing(bool editing)
    {
        var flyout = FlyoutBase.GetAttachedFlyout(EditAnchor);
        if (!editing)
        {
            flyout?.Hide();

            return;
        }

        if (_view?.EditRange is { } range && TextMarks.RangeBounds(TextView, range) is { } bounds)
        {
            Canvas.SetLeft(EditAnchor, bounds.X);
            Canvas.SetTop(EditAnchor, bounds.Y);
            EditAnchor.Width = bounds.Width;
            EditAnchor.Height = bounds.Height;
        }

        FlyoutBase.ShowAttachedFlyout(EditAnchor);
    }

    /// <summary>
    /// Puts the cursor into the field of a correction with its text selected, so typing replaces it.
    /// </summary>
    /// <param name="sender">The field.</param>
    /// <param name="e">The event arguments.</param>
    private void OnEditBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TextBox box)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    box.Focus();
                    box.SelectAll();
                },
                DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Saves the correction on Enter and drops it on Escape.
    /// </summary>
    /// <param name="sender">The field.</param>
    /// <param name="e">The event arguments.</param>
    private void OnEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (_view is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_view.SaveCorrectionCommand.CanExecute(null))
            {
                _view.SaveCorrectionCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _view.CancelCorrectionCommand.Execute(null);
        }
    }

    /// <summary>
    /// Drops the correction when the field closes by a click outside it.
    /// </summary>
    /// <param name="sender">The field.</param>
    /// <param name="e">The event arguments.</param>
    private void OnEditClosed(object? sender, EventArgs e)
    {
        if (_view?.IsEditing == true)
        {
            _view.CancelCorrectionCommand.Execute(null);
        }
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    /// <param name="sender">The button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
