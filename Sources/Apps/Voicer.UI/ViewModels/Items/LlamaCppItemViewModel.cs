using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Downloads;
using Voicer.Downloads.Enums;
using Voicer.Localization;
using Voicer.Speech.Llama.Tools;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// llama.cpp in the settings: the engine that runs the voice models. The application installs its pinned build by
/// itself, on every system llama.cpp publishes a build for.
/// </summary>
public sealed partial class LlamaCppItemViewModel : LocalizedViewModel
{
    /// <summary>
    /// The locator of llama.cpp.
    /// </summary>
    private readonly ILlamaCppLocator _locator;

    /// <summary>
    /// The installer of llama.cpp.
    /// </summary>
    private readonly ILlamaCppInstaller _installer;

    /// <summary>
    /// Stops the installation in progress, or <see langword="null"/>.
    /// </summary>
    private CancellationTokenSource? _installation;

    /// <summary>
    /// The path of llama-tts, or <see langword="null"/> when it is not installed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFound), nameof(StatusText), nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    private string? _path;

    /// <summary>
    /// Whether llama.cpp is being installed.
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
    /// Initializes a new instance of the <see cref="LlamaCppItemViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="locator">The locator of llama.cpp.</param>
    /// <param name="installer">The installer of llama.cpp.</param>
    public LlamaCppItemViewModel(ILocalizer localizer, ILlamaCppLocator locator, ILlamaCppInstaller installer)
        : base(localizer)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _path = locator.Locate();
    }

    /// <summary>
    /// Raised when llama.cpp has been installed.
    /// </summary>
    public event EventHandler? Installed;

    /// <summary>
    /// Gets a value indicating whether llama.cpp is installed.
    /// </summary>
    public bool IsFound => Path is not null;

    /// <summary>
    /// Gets a value indicating whether the application can install llama.cpp here and now.
    /// </summary>
    public bool CanInstall => !IsFound && _installer.CanInstall;

    /// <summary>
    /// Gets a value indicating whether there is no build of llama.cpp for this system.
    /// </summary>
    public bool IsUnsupported => !_installer.CanInstall;

    /// <summary>
    /// Gets the size of the download.
    /// </summary>
    public string SizeText => DisplayText.Bytes(Localizer, _installer.Build?.SizeBytes ?? 0);

    /// <summary>
    /// Gets a value indicating whether a failure is shown.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);

    /// <summary>
    /// Gets how much of the current stage of the installation is done, from 0 to 100.
    /// </summary>
    public double ProgressPercent => Progress?.Percent ?? 0;

    /// <summary>
    /// Gets the state of llama.cpp in words.
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

            if (IsFound)
            {
                return Localizer.Format("LlamaCpp.Found", LlamaCppPackage.VERSION);
            }

            return Localizer.Get(IsUnsupported ? "LlamaCpp.Unsupported" : "LlamaCpp.NotInstalled");
        }
    }

    /// <summary>
    /// Looks for llama.cpp again.
    /// </summary>
    public void Refresh()
    {
        Path = _locator.Locate();
    }

    /// <summary>
    /// Downloads and installs llama.cpp.
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
            if (IsFound)
            {
                Installed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // Nothing to report: the user stopped it.
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Error = Localizer.Format("LlamaCpp.InstallFailed", Localizer.Describe(ex));
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
    /// <returns><see langword="true"/> when llama.cpp is missing, can be installed and is not being installed.</returns>
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
