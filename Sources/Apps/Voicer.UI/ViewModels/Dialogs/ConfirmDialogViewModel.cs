namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// A question before an action that cannot be undone: the window shows it with the button that confirms it and a
/// button that cancels. A notice is only read: the window shows it with one button that closes it.
/// </summary>
/// <param name="Title">The title of the window.</param>
/// <param name="Message">What will happen, or what happened.</param>
/// <param name="ConfirmLabel">The text of the button that confirms the action, or closes a notice.</param>
/// <param name="IsNotice">Whether it is a notice to read, not a question.</param>
public sealed record ConfirmDialogViewModel(string Title, string Message, string ConfirmLabel, bool IsNotice = false);
