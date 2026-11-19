using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Downloads;
using Voicer.Downloads.Enums;
using Voicer.Localization;
using Voicer.UI.Services.Dialogs;
using Voicer.UI.ViewModels.Dialogs;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// The card of a model in the window of models: what it is, whether it is on this computer, and its download. A
/// downloaded model is lit and marked as received; a model that is not here says nothing about it.
/// </summary>
public abstract partial class ModelCardViewModel : LocalizedViewModel
{
    /// <summary>
    /// The dialogs that open the folder of the model.
    /// </summary>
    private readonly IDialogService _dialogs;

    /// <summary>
    /// Stops the download in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _download;

    /// <summary>
    /// Whether the model is downloaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasStatus), nameof(DownloadLabel))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(DeleteCommand))]
    private bool _isInstalled;

    /// <summary>
    /// Whether the model is being downloaded.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasStatus), nameof(CanChooseVariant))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(CancelCommand), nameof(DeleteCommand))]
    private bool _isDownloading;

    /// <summary>
    /// The progress of the download, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(HasStatus), nameof(ProgressPercent), nameof(IsVerifying))]
    private DownloadProgress? _progress;

    /// <summary>
    /// Why the last download failed, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelCardViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="dialogs">The dialogs that open the folder of the model.</param>
    protected ModelCardViewModel(ILocalizer localizer, IDialogService dialogs)
        : base(localizer)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    }

    /// <summary>
    /// Raised when the model has been downloaded and checked.
    /// </summary>
    public event EventHandler? Installed;

    /// <summary>
    /// Raised when the model has been deleted from the computer.
    /// </summary>
    public event EventHandler? Removed;

    /// <summary>
    /// Gets the name of the model.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the name of exactly the files on the disk, as the question before the deletion names them.
    /// </summary>
    public virtual string FullName => Name;

    /// <summary>
    /// Gets what the model is good at.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Gets the size of the download.
    /// </summary>
    public abstract string SizeText { get; }

    /// <summary>
    /// Gets the folder the model is kept in.
    /// </summary>
    public abstract string Folder { get; }

    /// <summary>
    /// Gets a value indicating whether the model works on the processor alone: an advantage the card shows.
    /// </summary>
    public abstract bool RunsOnCpu { get; }

    /// <summary>
    /// Gets a value indicating whether another variant of the model can be chosen: not while a download runs.
    /// </summary>
    public bool CanChooseVariant => !IsDownloading;

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets a value indicating whether the received file is being checked.
    /// </summary>
    public bool IsVerifying => Progress?.Stage == DownloadStage.Verifying;

    /// <summary>
    /// Gets how much of the current stage of the download is done, from 0 to 100.
    /// </summary>
    public double ProgressPercent => Progress?.Percent ?? 0;

    /// <summary>
    /// Gets the state of the model in words: received, being received or paused; nothing for a model that is not here.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (IsInstalled)
            {
                return Localizer.Get("Models.Installed");
            }

            if (IsDownloading)
            {
                return ProgressText();
            }

            var received = ReceivedBytes();

            return received > 0
                ? Localizer.Format("Models.Paused", DisplayText.Bytes(Localizer, received), SizeText)
                : string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is a state to show.
    /// </summary>
    public bool HasStatus => StatusText.Length > 0;

    /// <summary>
    /// Gets the text of the download button: a download that was interrupted is continued.
    /// </summary>
    public string DownloadLabel => Localizer.Get(ReceivedBytes() > 0 ? "Models.Resume" : "Models.Download");

    /// <summary>
    /// Downloads the model.
    /// </summary>
    /// <param name="progress">Receives the progress.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <returns>A task that completes when the model is downloaded and checked.</returns>
    protected abstract Task InstallAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Looks whether the model is downloaded now.
    /// </summary>
    /// <returns><see langword="true"/> when it is.</returns>
    protected abstract bool CheckInstalled();

    /// <summary>
    /// Deletes the files of the model from the disk.
    /// </summary>
    /// <exception cref="IOException">A file is in use or the disk failed.</exception>
    /// <exception cref="UnauthorizedAccessException">A file may not be deleted.</exception>
    protected abstract void DeleteFiles();

    /// <summary>
    /// Tells how much of the model an interrupted download has received.
    /// </summary>
    /// <returns>The received bytes, or <c>0</c>.</returns>
    protected abstract long ReceivedBytes();

    /// <summary>
    /// Announces that the state of the model changed outside the download (another variant was chosen).
    /// </summary>
    protected void RefreshState()
    {
        Error = null;
        IsInstalled = CheckInstalled();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
        OnPropertyChanged(nameof(DownloadLabel));
    }

    /// <summary>
    /// Downloads the model.
    /// </summary>
    /// <returns>A task that completes when the download ends.</returns>
    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync()
    {
        Error = null;
        IsDownloading = true;
        _download = new CancellationTokenSource();
        try
        {
            await InstallAsync(new Progress<DownloadProgress>(p => Progress = p), _download.Token);
            IsInstalled = CheckInstalled();
            if (IsInstalled)
            {
                Installed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // The received part stays: the next download continues from it.
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Error = Localizer.Format("Models.DownloadFailed", Localizer.Describe(ex));
        }
        finally
        {
            _download.Dispose();
            _download = null;
            IsDownloading = false;
            Progress = null;
            OnPropertyChanged(nameof(DownloadLabel));
        }
    }

    /// <summary>
    /// Tells whether the model can be downloaded now.
    /// </summary>
    /// <returns><see langword="true"/> when it is not there and nothing is being downloaded.</returns>
    private bool CanDownload()
    {
        return !IsInstalled && !IsDownloading;
    }

    /// <summary>
    /// Stops the download; what was received stays for the next time.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDownloading))]
    private void Cancel()
    {
        _download?.Cancel();
    }

    /// <summary>
    /// Deletes the model from the computer once the user confirms it; a file in use is reported on the card.
    /// </summary>
    /// <returns>A task that completes when the model is deleted or kept.</returns>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var question = new ConfirmDialogViewModel(
            Localizer.Get("Models.DeleteTitle"),
            Localizer.Format("Models.DeleteMessage", FullName, SizeText),
            Localizer.Get("Models.DeleteConfirm"));
        if (!await _dialogs.ConfirmAsync(question))
        {
            return;
        }

        Exception? failure = null;
        try
        {
            await Task.Run(DeleteFiles);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failure = ex;
        }

        // What is left on the disk decides: a file that could not go keeps the model installed.
        RefreshState();
        if (failure is not null)
        {
            Error = Localizer.Format("Models.DeleteFailed", Localizer.Describe(failure));
        }

        Removed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Tells whether the model can be deleted now.
    /// </summary>
    /// <returns><see langword="true"/> when it is downloaded and nothing is being downloaded.</returns>
    private bool CanDelete()
    {
        return IsInstalled && !IsDownloading;
    }

    /// <summary>
    /// Opens the folder of the model in the file manager.
    /// </summary>
    /// <returns>A task that completes when the folder is opened.</returns>
    [RelayCommand]
    private Task OpenFolderAsync()
    {
        return _dialogs.OpenFolderAsync(Folder);
    }

    /// <summary>
    /// Words the progress of the download.
    /// </summary>
    /// <returns>The text.</returns>
    private string ProgressText()
    {
        if (Progress is not { } progress)
        {
            return Localizer.Get("Models.Starting");
        }

        if (progress.Stage == DownloadStage.Verifying)
        {
            return Localizer.Format("Models.Verifying", progress.Percent);
        }

        var received = Localizer.Format(
            "Models.Received",
            DisplayText.Bytes(Localizer, progress.DoneBytes),
            DisplayText.Bytes(Localizer, progress.TotalBytes));

        return progress.BytesPerSecond > 0
            ? Localizer.Format("Models.ReceivedWithSpeed", received, DisplayText.Speed(Localizer, progress.BytesPerSecond))
            : received;
    }
}
