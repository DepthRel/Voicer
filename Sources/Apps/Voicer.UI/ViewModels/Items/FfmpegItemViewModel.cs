using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Downloads;
using Voicer.Downloads.Enums;
using Voicer.Localization;
using Voicer.Media.Ffmpeg;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// FFmpeg in the settings: where it was found, or how to get it. On Windows the application installs it by itself; on
/// Linux and macOS the user installs it with the package manager.
/// </summary>
public sealed partial class FfmpegItemViewModel : LocalizedViewModel
{
    /// <summary>
    /// The locator of FFmpeg.
    /// </summary>
    private readonly IFfmpegLocator _locator;

    /// <summary>
    /// The installer of FFmpeg.
    /// </summary>
    private readonly IFfmpegInstaller _installer;

    /// <summary>
    /// Stops the installation in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _installation;

    /// <summary>
    /// The path of the program, or <see langword="null"/> when it was not found.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFound), nameof(IsMissing), nameof(StatusText), nameof(CanInstall), nameof(ShowHint))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private string? _path;

    /// <summary>
    /// Whether FFmpeg is being installed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(CancelCommand))]
    private bool _isInstalling;

    /// <summary>
    /// The progress of the installation, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(ProgressPercent))]
    private DownloadProgress? _progress;

    /// <summary>
    /// Why the last installation failed, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegItemViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="locator">The locator of FFmpeg.</param>
    /// <param name="installer">The installer of FFmpeg.</param>
    public FfmpegItemViewModel(ILocalizer localizer, IFfmpegLocator locator, IFfmpegInstaller installer)
        : base(localizer)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _path = locator.Locate();
    }

    /// <summary>
    /// Gets a value indicating whether FFmpeg was found.
    /// </summary>
    public bool IsFound => Path is not null;

    /// <summary>
    /// Gets a value indicating whether FFmpeg is missing.
    /// </summary>
    public bool IsMissing => Path is null;

    /// <summary>
    /// Gets a value indicating whether the application can install FFmpeg here and now.
    /// </summary>
    public bool CanInstall => IsMissing && _installer.CanInstall;

    /// <summary>
    /// Gets a value indicating whether the user installs FFmpeg by hand: it is missing on a system where the application
    /// cannot install it.
    /// </summary>
    public bool ShowHint => IsMissing && !_installer.CanInstall;

    /// <summary>
    /// Gets the command that installs FFmpeg on this system.
    /// </summary>
    public string Hint => Localizer.Get(OperatingSystem.IsMacOS() ? "Ffmpeg.HintMac" : "Ffmpeg.HintLinux");

    /// <summary>
    /// Gets the size of the download on Windows.
    /// </summary>
    public string SizeText => DisplayText.Bytes(Localizer, FfmpegPackage.SIZE_BYTES);

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets how much of the current stage of the installation is done, from 0 to 100.
    /// </summary>
    public double ProgressPercent => Progress?.Percent ?? 0;

    /// <summary>
    /// Gets the state of FFmpeg in words.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (IsInstalling)
            {
                return Progress switch
                {
                    { Stage: DownloadStage.Verifying } p => Localizer.Format("Models.Verifying", p.Percent),
                    { Stage: DownloadStage.Installing } => Localizer.Get("Ffmpeg.Unpacking"),
                    { } p => Localizer.Format("Models.Received", DisplayText.Bytes(Localizer, p.DoneBytes), DisplayText.Bytes(Localizer, p.TotalBytes)),
                    _ => Localizer.Get("Models.Starting"),
                };
            }

            return IsFound ? Localizer.Format("Ffmpeg.Found", Path) : Localizer.Get("Ffmpeg.NotFound");
        }
    }

    /// <summary>
    /// Looks for FFmpeg again (the user installed it meanwhile).
    /// </summary>
    [RelayCommand]
    private void Refresh()
    {
        Path = _locator.Locate();
    }

    /// <summary>
    /// Downloads and installs FFmpeg.
    /// </summary>
    /// <returns>A task that completes when the installation ends.</returns>
    [RelayCommand(CanExecute = nameof(CanStartInstall))]
    private async Task InstallAsync()
    {
        Error = null;
        IsInstalling = true;
        _installation = new CancellationTokenSource();
        try
        {
            await _installer.InstallAsync(new Progress<DownloadProgress>(p => Progress = p), _installation.Token);
            Path = _locator.Locate();
        }
        catch (OperationCanceledException)
        {
            // Nothing to report: the user stopped it.
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Error = Localizer.Format("Ffmpeg.InstallFailed", Localizer.Describe(ex));
        }
        finally
        {
            _installation.Dispose();
            _installation = null;
            IsInstalling = false;
            Progress = null;
        }
    }

    /// <summary>
    /// Tells whether the installation can start now.
    /// </summary>
    /// <returns><see langword="true"/> when FFmpeg is missing, can be installed and is not being installed.</returns>
    private bool CanStartInstall()
    {
        return CanInstall && !IsInstalling;
    }

    /// <summary>
    /// Stops the installation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsInstalling))]
    private void Cancel()
    {
        _installation?.Cancel();
    }
}
