using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.Views.Dialogs;

namespace Voicer.UI.Services.Dialogs;

/// <summary>
/// <see cref="IDialogService"/> with Avalonia windows and the storage pickers of the platform.
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    /// <summary>
    /// Returns the window that owns the dialogs.
    /// </summary>
    private readonly Func<Window?> _owner;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaDialogService"/> class.
    /// </summary>
    /// <param name="owner">Returns the window that owns the dialogs.</param>
    public AvaloniaDialogService(Func<Window?> owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <inheritdoc />
    public async Task<string?> PickMediaFileAsync(string title, string filterName, IReadOnlyList<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        if (TopWindow() is not { } window)
        {
            return null;
        }

        var media = new FilePickerFileType(filterName) { Patterns = [.. extensions.Select(e => "*" + e)] };
        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [media, FilePickerFileTypes.All],
        });

        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string filterName, string extension)
    {
        if (TopWindow() is not { } window)
        {
            return null;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = ["*." + extension] }],
        });

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task ShowRecordAsync(RecordViewModel record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (_owner() is not { } owner)
        {
            return;
        }

        await new RecordWindow { DataContext = record }.ShowDialog(owner);
    }

    /// <inheritdoc />
    public async Task ShowModelsAsync(ModelCatalogViewModel catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (_owner() is not { } owner)
        {
            return;
        }

        await new ModelsWindow { DataContext = catalog }.ShowDialog(owner);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(ConfirmDialogViewModel question)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (TopWindow() is not { } owner)
        {
            return false;
        }

        return await new ConfirmDialog { DataContext = question }.ShowDialog<bool>(owner);
    }

    /// <inheritdoc />
    public async Task<bool> OpenFolderAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_owner() is not { } owner)
        {
            return false;
        }

        Directory.CreateDirectory(path);
        var folder = await owner.StorageProvider.TryGetFolderFromPathAsync(path);

        return folder is not null && await owner.Launcher.LaunchFileAsync(folder);
    }

    /// <summary>
    /// Finds the window on top: a picker opened from a dialog belongs to that dialog, not to the window under it.
    /// </summary>
    /// <returns>The top window, or <see langword="null"/>.</returns>
    private Window? TopWindow()
    {
        var window = _owner();
        while (window?.OwnedWindows.LastOrDefault() is { } owned)
        {
            window = owned;
        }

        return window;
    }
}
