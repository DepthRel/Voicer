namespace Voicer.UI.ViewModels.Dialogs;

/// <summary>
/// A speed of the player in the list of speeds.
/// </summary>
/// <param name="Factor">The speed: <c>1</c> as recorded.</param>
/// <param name="Label">The speed in words.</param>
public sealed record SpeedOptionViewModel(double Factor, string Label);
