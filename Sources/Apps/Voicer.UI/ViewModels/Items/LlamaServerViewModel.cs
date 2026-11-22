using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Voicer.Localization;
using Voicer.Speech.Llama.Enums;
using Voicer.Speech.Llama.Servers;
using Voicer.UI.ViewModels.Formatting;

namespace Voicer.UI.ViewModels.Items;

/// <summary>
/// A llama-server of the application as the user sees it, in the settings and in the tab strip: whether it runs and with
/// which model, what it takes from the computer, when it stops by itself, and a button that stops it at once.
/// </summary>
public sealed partial class LlamaServerViewModel : LocalizedViewModel
{
    /// <summary>
    /// What is shown for a share of the processor not measured yet.
    /// </summary>
    private const string UNKNOWN = "—";

    /// <summary>
    /// Reads the state of the server and stops it.
    /// </summary>
    private readonly ILlamaServerMonitor _monitor;

    /// <summary>
    /// The clock of the measures.
    /// </summary>
    private readonly TimeProvider _time;

    /// <summary>
    /// How many cores the processor has: the share of the processor is of all of them.
    /// </summary>
    private readonly int _cores;

    /// <summary>
    /// The last measure of the processor time: when, of which process, how much.
    /// </summary>
    private (DateTimeOffset At, int ProcessId, TimeSpan Used)? _lastMeasure;

    /// <summary>
    /// The state read last.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsActive), nameof(CanStop), nameof(ChipText), nameof(StateText), nameof(UsageText), nameof(HasUsage))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private LlamaServerStatus _status = LlamaServerStatus.Stopped;

    /// <summary>
    /// The share of the processor the server took since the last measure, in percent, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsageText))]
    private double? _processorPercent;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlamaServerViewModel"/> class.
    /// </summary>
    /// <param name="localizer">The localizer of the texts.</param>
    /// <param name="monitor">Reads the state of the server and stops it.</param>
    /// <param name="time">The clock of the measures.</param>
    /// <param name="cores">How many cores the processor has; the cores of this computer when not given.</param>
    public LlamaServerViewModel(ILocalizer localizer, ILlamaServerMonitor monitor, TimeProvider time, int? cores = null)
        : base(localizer)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _cores = Math.Max(1, cores ?? Environment.ProcessorCount);
    }

    /// <summary>
    /// Gets a value indicating whether a server runs: it waits or searches.
    /// </summary>
    public bool IsRunning => Status.State is LlamaServerState.Idle or LlamaServerState.Busy;

    /// <summary>
    /// Gets a value indicating whether a model is in memory or loading into it: the tab strip shows it.
    /// </summary>
    public bool IsActive => Status.State != LlamaServerState.Stopped;

    /// <summary>
    /// Gets a value indicating whether the server can be stopped now: it runs or starts, even while a request uses it.
    /// </summary>
    public bool CanStop => IsActive;

    /// <summary>
    /// Gets the short text of the tab strip: the model and the memory it takes, or only the model while it loads.
    /// </summary>
    public string ChipText => Status.Usage is { } usage
        ? Localizer.Format("LlamaServer.Chip", Status.ModelName ?? string.Empty, DisplayText.Bytes(Localizer, usage.MemoryBytes))
        : Status.ModelName ?? string.Empty;

    /// <summary>
    /// Gets what the server is doing, in words.
    /// </summary>
    public string StateText => Status.State switch
    {
        LlamaServerState.Starting => Localizer.Format("LlamaServer.Starting", Status.ModelName ?? string.Empty),
        LlamaServerState.Busy => Localizer.Format("LlamaServer.Busy", Status.ModelName ?? string.Empty),
        LlamaServerState.Idle when Status.StopsAt is { } stopsAt => Localizer.Format(
            "LlamaServer.Idle",
            Status.ModelName ?? string.Empty,
            DisplayText.Duration(Max(stopsAt - _time.GetUtcNow(), TimeSpan.Zero))),
        LlamaServerState.Idle => Localizer.Format("LlamaServer.Running", Status.ModelName ?? string.Empty),
        _ => Localizer.Get("LlamaServer.Stopped"),
    };

    /// <summary>
    /// Gets what the server takes from the computer, or an empty text when none runs.
    /// </summary>
    public string UsageText => Status.Usage is { } usage
        ? Localizer.Format(
            "LlamaServer.Usage",
            DisplayText.Bytes(Localizer, usage.MemoryBytes),
            ProcessorPercent is { } percent ? percent.ToString("0", Localizer.Culture) : UNKNOWN,
            usage.ProcessId)
        : string.Empty;

    /// <summary>
    /// Gets a value indicating whether the usage of the server is shown.
    /// </summary>
    public bool HasUsage => Status.Usage is not null;

    /// <summary>
    /// Reads the state of the server again and measures its share of the processor since the last reading.
    /// </summary>
    public void Refresh()
    {
        var status = _monitor.Status();
        var now = _time.GetUtcNow();

        double? percent = null;
        if (status.Usage is { } usage)
        {
            if (_lastMeasure is { } last && last.ProcessId == usage.ProcessId && now > last.At)
            {
                var used = (usage.ProcessorTime - last.Used).TotalSeconds;
                percent = Math.Clamp(used / ((now - last.At).TotalSeconds * _cores) * 100, 0, 100);
            }

            _lastMeasure = (now, usage.ProcessId, usage.ProcessorTime);
        }
        else
        {
            _lastMeasure = null;
        }

        ProcessorPercent = percent;
        Status = status;

        // The time left changes even when the state does not.
        OnPropertyChanged(nameof(StateText));
    }

    /// <summary>
    /// Stops the server at once, giving its memory back, and shows what came of it: a request it was answering fails,
    /// and the next one starts the server again.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    public void Stop()
    {
        _monitor.Stop();
        Refresh();
    }

    /// <summary>
    /// Takes the longer of two spans.
    /// </summary>
    /// <param name="a">The first span.</param>
    /// <param name="b">The second span.</param>
    /// <returns>The longer one.</returns>
    private static TimeSpan Max(TimeSpan a, TimeSpan b)
    {
        return a > b ? a : b;
    }
}
