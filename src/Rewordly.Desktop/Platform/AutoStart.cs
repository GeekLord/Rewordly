using System.Security;
using Microsoft.Win32;

namespace Rewordly.Desktop.Platform;

/// <summary>Start at sign-in: registry Run key (Windows), LaunchAgent (macOS), XDG autostart entry (Linux).</summary>
internal static class AutoStart
{
    private static string Exe => Environment.ProcessPath ?? throw new InvalidOperationException("Unknown executable path.");

    public static void Apply(bool enable)
    {
        if (Os.IsWindows) Windows(enable);
        else if (Os.IsMac) Mac(enable);
        else Linux(enable);
    }

    private static void Windows(bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enable) key.SetValue("Rewordly", $"\"{Exe}\" --background");
        else key.DeleteValue("Rewordly", throwOnMissingValue: false);
    }

    private static void Mac(bool enable)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "com.rewordly.app.plist");
        if (!enable) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key><string>com.rewordly.app</string>
              <key>ProgramArguments</key>
              <array><string>{SecurityElement.Escape(Exe)}</string><string>--background</string></array>
              <key>RunAtLoad</key><true/>
              <key>ProcessType</key><string>Interactive</string>
            </dict>
            </plist>
            """);
    }

    private static void Linux(bool enable)
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var dir = Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") : xdg, "autostart");
        var path = Path.Combine(dir, "rewordly.desktop");
        if (!enable) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, $"""
            [Desktop Entry]
            Type=Application
            Name=Rewordly
            Comment=AI text rewriting from any app
            Exec="{Exe}" --background
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """);
    }
}
