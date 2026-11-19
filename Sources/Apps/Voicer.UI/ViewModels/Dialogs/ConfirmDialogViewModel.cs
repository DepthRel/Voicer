namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// A question before an action that cannot be undone: the window shows it with the button that confirms it and a
/// button that cancels.
/// </summary>
/// <param name="Title">The title of the window.</param>
/// <param name="Message">What will happen.</param>
/// <param name="ConfirmLabel">The text of the button that confirms the action.</param>
public sealed record ConfirmDialogViewModel(string Title, string Message, string ConfirmLabel);
