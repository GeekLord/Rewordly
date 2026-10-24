using System.IO.Pipes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Rewordly.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        using var instanceLock = TryLock();
        if (instanceLock == null)
        {
            // Already running: ask the running copy to show its settings (or chat) window.
            try
            {
                using var c = new NamedPipeClientStream(".", AppController.PipeName, PipeDirection.Out);
                c.Connect(1500);
                using var w = new StreamWriter(c);
                w.WriteLine(args.Contains("--chat") ? "chat" : "show");
            }
            catch { }
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    /// <summary>
    /// One running copy per user: an exclusively opened lock file (flock on macOS/Linux, share-mode lock on Windows).
    /// The OS releases it automatically if the app exits or crashes.
    /// </summary>
    private static FileStream? TryLock()
    {
        var dir = Rewordly.Core.SettingsStore.DefaultFolder;
        try
        {
            Directory.CreateDirectory(dir);
            return new FileStream(Path.Combine(dir, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .With(new MacOSPlatformOptions { ShowInDock = false })
        .With(new X11PlatformOptions { EnableIme = true })
        .LogToTrace();
}

internal sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Name = "Rewordly";
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception ex) Platform.Log.Error("domain", ex); };
            TaskScheduler.UnobservedTaskException += (_, e) => { Platform.Log.Error("task", e.Exception); e.SetObserved(); };
            _ = new AppController(desktop, desktop.Args?.Contains("--background") == true);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
