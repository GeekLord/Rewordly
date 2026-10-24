using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Rewordly.Desktop.Platform;

/// <summary>Small OS helpers shared by the platform services.</summary>
internal static class Os
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsMac => OperatingSystem.IsMacOS();
    public static bool IsLinux => OperatingSystem.IsLinux();

    public static string Name => IsWindows ? "Windows" : IsMac ? "macOS" : "Linux";

    /// <summary>The copy/paste modifier: Cmd on macOS, Ctrl elsewhere.</summary>
    public static string ModLabel => IsMac ? "Cmd" : "Ctrl";

    /// <summary>Label for the alternative shortcut.</summary>
    public static string AltShortcutLabel => IsMac ? "Ctrl+Cmd+C" : IsLinux ? "Ctrl+Alt+C" : "Ctrl+Insert";

    public static bool IsWayland =>
        IsLinux && (string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    public static bool HasCommand(string name)
    {
        if (IsWindows) return false;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
            if (dir.Length > 0 && File.Exists(Path.Combine(dir, name))) return true;
        return false;
    }

    public sealed record ProcResult(int ExitCode, string StdOut, string StdErr);

    /// <summary>Runs a command and captures its output. Arguments are passed without a shell.</summary>
    public static ProcResult Run(string file, IEnumerable<string> args, string? stdin = null, int timeoutMs = 5000)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin != null,
            UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}.");
        if (stdin != null)
        {
            p.StandardInput.Write(stdin);
            p.StandardInput.Close();
        }
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(true); } catch { }
            return new ProcResult(-1, "", "timeout");
        }
        return new ProcResult(p.ExitCode, outTask.Result, errTask.Result);
    }

    public static void Open(string target)
    {
        try
        {
            if (IsWindows) Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            else if (IsMac) Process.Start("open", new[] { target });
            else Process.Start("xdg-open", new[] { target });
        }
        catch (Exception ex) { Log.Error("open", ex); }
    }

    // --- POSIX signals (pause/resume speech processes) -------------------------------------
    [DllImport("libc", SetLastError = true, EntryPoint = "kill")] private static extern int sys_kill(int pid, int sig);

    public static void Pause(Process p) { if (!IsWindows) sys_kill(p.Id, IsMac ? 17 : 19); }   // SIGSTOP
    public static void Resume(Process p) { if (!IsWindows) sys_kill(p.Id, IsMac ? 19 : 18); }  // SIGCONT

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)] private static extern int sys_chmod(string path, int mode);

    /// <summary>Restricts a file to the current user (0600) on macOS/Linux.</summary>
    public static void MakePrivate(string path)
    {
        if (!IsWindows) { try { sys_chmod(path, Convert.ToInt32("600", 8)); } catch { } }
    }
}

internal static class Log
{
    private static readonly object Gate = new();
    public static string? Folder { get; set; }

    /// <summary>Writes errors only (never the user's text) to error.log.</summary>
    public static void Error(string where, Exception ex) => Write($"[{where}] {ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");

    public static void Info(string message) => Write(message);

    private static void Write(string message)
    {
        try
        {
            if (Folder == null) return;
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, "error.log");
                if (File.Exists(path) && new FileInfo(path).Length > 512 * 1024) File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:u} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
