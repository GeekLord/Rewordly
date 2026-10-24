using System.Runtime.InteropServices;

namespace Rewordly.Desktop.Platform;

/// <summary>
/// Remembers which app/window had focus when the shortcut fired and gives focus back to it before pasting.
/// Windows: HWND · macOS: process id (NSRunningApplication) · Linux X11: window id via xdotool (optional).
/// </summary>
internal static class WindowFocus
{
    public readonly record struct Target(long Id)
    {
        public bool IsEmpty => Id == 0;
        public static Target None => new(0);
    }

    public static Target Capture()
    {
        try
        {
            if (Os.IsWindows) return new Target(Win.GetForegroundWindow().ToInt64());
            if (Os.IsMac) return new Target(Mac.FrontmostPid());
            if (Os.IsLinux && Os.HasCommand("xdotool"))
            {
                var r = Os.Run("xdotool", new[] { "getactivewindow" }, timeoutMs: 1500);
                if (r.ExitCode == 0 && long.TryParse(r.StdOut.Trim(), out var id)) return new Target(id);
            }
        }
        catch (Exception ex) { Log.Error("focus.capture", ex); }
        return Target.None;
    }

    public static bool IsOwn(Target t)
    {
        if (t.IsEmpty) return false;
        try
        {
            if (Os.IsWindows) { Win.GetWindowThreadProcessId(new IntPtr(t.Id), out var pid); return pid == Environment.ProcessId; }
            if (Os.IsMac) return t.Id == Environment.ProcessId;
            if (Os.IsLinux && Os.HasCommand("xdotool"))
            {
                var r = Os.Run("xdotool", new[] { "getwindowpid", t.Id.ToString() }, timeoutMs: 1500);
                return r.ExitCode == 0 && r.StdOut.Trim() == Environment.ProcessId.ToString();
            }
        }
        catch { }
        return false;
    }

    public static void Restore(Target t)
    {
        if (t.IsEmpty) return;
        try
        {
            if (Os.IsWindows) Win.SetForegroundWindow(new IntPtr(t.Id));
            else if (Os.IsMac) Mac.Activate((int)t.Id);
            else if (Os.IsLinux && Os.HasCommand("xdotool"))
                Os.Run("xdotool", new[] { "windowactivate", "--sync", t.Id.ToString() }, timeoutMs: 2000);
        }
        catch (Exception ex) { Log.Error("focus.restore", ex); }
    }

    /// <summary>Windows only: lets one of our windows take focus while another app is in front.</summary>
    public static void ForceForeground(IntPtr ownHwnd)
    {
        if (!Os.IsWindows || ownHwnd == IntPtr.Zero) return;
        var fg = Win.GetForegroundWindow();
        uint fgThread = fg == IntPtr.Zero ? 0 : Win.GetWindowThreadProcessId(fg, out _);
        uint me = Win.GetCurrentThreadId();
        if (fgThread != 0 && fgThread != me)
        {
            Win.AttachThreadInput(me, fgThread, true);
            Win.BringWindowToTop(ownHwnd);
            Win.SetForegroundWindow(ownHwnd);
            Win.AttachThreadInput(me, fgThread, false);
        }
        else Win.SetForegroundWindow(ownHwnd);
    }

    private static class Win
    {
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    }

    /// <summary>Minimal Objective-C runtime calls into AppKit.</summary>
    private static class Mac
    {
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr recv, IntPtr sel);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern int SendInt(IntPtr recv, IntPtr sel);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendPid(IntPtr recv, IntPtr sel, int pid);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool SendUlong(IntPtr recv, IntPtr sel, ulong v);

        public static long FrontmostPid()
        {
            var ws = Send(objc_getClass("NSWorkspace"), sel_registerName("sharedWorkspace"));
            var app = Send(ws, sel_registerName("frontmostApplication"));
            return app == IntPtr.Zero ? 0 : SendInt(app, sel_registerName("processIdentifier"));
        }

        public static void Activate(int pid)
        {
            var app = SendPid(objc_getClass("NSRunningApplication"), sel_registerName("runningApplicationWithProcessIdentifier:"), pid);
            if (app != IntPtr.Zero) SendUlong(app, sel_registerName("activateWithOptions:"), 2 /* IgnoringOtherApps */);
        }
    }
}
