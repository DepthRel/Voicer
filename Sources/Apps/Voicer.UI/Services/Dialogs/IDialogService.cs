using Voicer.UI.ViewModels.Dialogs;

namespace Voicer.UI.Services.Dialogs;

/// <summary>
/// Windows and file pickers the view models open.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Picks an audio or video file to recognize.
    /// </summary>
    /// <param name="title">The title of the picker.</param>
    /// <param name="filterName">The name of the filter of the media files.</param>
    /// <param name="extensions">The extensions of the media files, with the leading dot.</param>
    /// <returns>The full path of the file, or <see langword="null"/> when nothing was picked.</returns>
    Task<string?> PickMediaFileAsync(string title, string filterName, IReadOnlyList<string> extensions);

    /// <summary>
    /// Picks where to save a file.
    /// </summary>
    /// <param name="title">The title of the picker.</param>
    /// <param name="suggestedName">The suggested file name.</param>
    /// <param name="filterName">The name of the type of the file.</param>
    /// <param name="extension">The extension of the file, without the dot.</param>
    /// <returns>The full path of the file, or <see langword="null"/> when nothing was picked.</returns>
    Task<string?> PickSaveFileAsync(string title, string suggestedName, string filterName, string extension);

    /// <summary>
    /// Shows the window of a record of the history.
    /// </summary>
    /// <param name="record">The view model of the window.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    Task ShowRecordAsync(RecordViewModel record);

    /// <summary>
    /// Shows the window of a catalog of models, with their cards and downloads.
    /// </summary>
    /// <param name="catalog">The view model of the window.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    Task ShowModelsAsync(ModelCatalogViewModel catalog);

    /// <summary>
    /// Asks the user to confirm an action that cannot be undone.
    /// </summary>
    /// <param name="question">What is asked.</param>
    /// <returns><see langword="true"/> when the user confirmed it.</returns>
    Task<bool> ConfirmAsync(ConfirmDialogViewModel question);

    /// <summary>
    /// Opens a folder in the file manager of the system.
    /// </summary>
    /// <param name="path">The folder; it is created when it is missing.</param>
    /// <returns><see langword="true"/> when the system opened it.</returns>
    Task<bool> OpenFolderAsync(string path);
}
