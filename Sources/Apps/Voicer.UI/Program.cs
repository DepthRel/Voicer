using Avalonia;
using Voicer.Core.Protocol;
using Voicer.Speech.Whisper.Worker;

namespace Voicer.UI;

/// <summary>
/// Entry point of Voicer: the window, or the worker process that runs the speech engine.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the application. With <see cref="WorkerProtocol.WORKER_SWITCH"/> as the first argument the program is the
    /// worker of an application that is already running and opens no window.
    /// </summary>
    /// <param name="args">
    /// Command-line arguments: <c>--data &lt;folder&gt;</c> names the data folder; <c>--worker …</c> starts the worker.
    /// </param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == WorkerProtocol.WORKER_SWITCH)
        {
            return WorkerHost.RunConsoleAsync(args[1..]).GetAwaiter().GetResult();
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Configures Avalonia; also used by the XAML previewer.
    /// </summary>
    /// <returns>The application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
