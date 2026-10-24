using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace Rewordly.Desktop.Platform;

/// <summary>
/// System-wide shortcut detection and key simulation (libuiohook via SharpHook):
/// Windows low-level hooks, macOS event taps (needs Accessibility permission), Linux X11 XRecord/XTest.
///
/// Only modifier and C / Insert state is tracked; no other keystrokes are recorded.
/// Events simulated by Rewordly itself are ignored.
/// </summary>
internal sealed class GlobalInput : IDisposable
{
    private readonly SimpleGlobalHook _hook;
    private readonly EventSimulator _sim = new();
    private readonly object _gate = new();
    private bool _modDown, _ctrlDown, _metaDown, _altDown, _cDown;
    private int _copies;
    private long _firstCopyTick;

    public int WindowMs { get; set; } = 800;
    public bool DoubleCopyEnabled { get; set; } = true;
    public bool AltShortcutEnabled { get; set; } = true;
    public bool Paused { get; set; }

    /// <summary>Last global pointer position (screen coordinates as used by Avalonia window positions).</summary>
    public (int X, int Y) Pointer { get; private set; } = (200, 200);
    public bool HasPointer { get; private set; }

    /// <summary>Raised on the hook thread; marshal to the UI thread before touching UI.</summary>
    public event Action? DoubleCopy;
    public event Action? AltShortcut;
    public event Action? MousePressed;
    public event Action<Exception>? Failed;

    public bool IsRunning => _hook.IsRunning;

    public GlobalInput()
    {
        _hook = new SimpleGlobalHook(GlobalHookType.All, null, runAsyncOnBackgroundThread: true);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MouseMoved += (_, e) => TrackPointer(e.Data.X, e.Data.Y);
        _hook.MouseDragged += (_, e) => TrackPointer(e.Data.X, e.Data.Y);
        _hook.MousePressed += (_, e) => { TrackPointer(e.Data.X, e.Data.Y); if (!e.IsEventSimulated) MousePressed?.Invoke(); };
    }

    /// <summary>macOS: true if the app may observe and send input. Optionally shows the system prompt.</summary>
    public static bool HasAccessibility(bool prompt)
    {
        if (!Os.IsMac) return true;
        try { return UioHookProvider.Instance.IsAxApiEnabled(prompt); }
        catch { return false; }
    }

    public void Start()
    {
        var task = _hook.RunAsync();
        task.ContinueWith(t => { if (t.Exception != null) Failed?.Invoke(t.Exception.GetBaseException()); }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void TrackPointer(short x, short y)
    {
        Pointer = (x, y);
        HasPointer = true;
    }

    private static bool IsCtrl(KeyCode k) => k is KeyCode.VcLeftControl or KeyCode.VcRightControl;
    private static bool IsMeta(KeyCode k) => k is KeyCode.VcLeftMeta or KeyCode.VcRightMeta;
    private static bool IsCopyMod(KeyCode k) => Os.IsMac ? IsMeta(k) : IsCtrl(k);

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated) return;
        var k = e.Data.KeyCode;
        bool fireDouble = false, fireAlt = false;
        lock (_gate)
        {
            if (IsCtrl(k)) _ctrlDown = true;
            if (IsMeta(k)) _metaDown = true;
            if (k is KeyCode.VcLeftAlt or KeyCode.VcRightAlt) _altDown = true;
            if (IsCopyMod(k)) { _modDown = true; return; }

            // Alternative shortcut: Ctrl+Insert (Windows), Ctrl+Cmd+C (macOS), Ctrl+Alt+C (Linux).
            bool alt = Os.IsMac ? (k == KeyCode.VcC && _ctrlDown && _metaDown)
                     : Os.IsLinux ? (k == KeyCode.VcC && _ctrlDown && _altDown)
                     : (k == KeyCode.VcInsert && _ctrlDown);
            if (alt)
            {
                if (AltShortcutEnabled && !Paused)
                {
                    fireAlt = true;
                    e.SuppressEvent = true; // honoured on Windows and macOS; on Linux apps still see the key (most ignore Ctrl+Alt+C)
                }
                _copies = 0;
            }
            else if (k == KeyCode.VcC && _modDown && !_cDown)
            {
                _cDown = true;
                long now = Environment.TickCount64;
                if (_copies == 1 && now - _firstCopyTick <= WindowMs)
                {
                    _copies = 0;
                    fireDouble = DoubleCopyEnabled && !Paused;
                }
                else
                {
                    _copies = 1;
                    _firstCopyTick = now;
                }
            }
        }
        if (fireAlt) AltShortcut?.Invoke();
        if (fireDouble) DoubleCopy?.Invoke();
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated) return;
        var k = e.Data.KeyCode;
        lock (_gate)
        {
            if (IsCtrl(k)) _ctrlDown = false;
            if (IsMeta(k)) _metaDown = false;
            if (k is KeyCode.VcLeftAlt or KeyCode.VcRightAlt) _altDown = false;
            if (IsCopyMod(k)) { _modDown = false; _cDown = false; _copies = 0; }
            else if (k == KeyCode.VcC) _cDown = false;
        }
    }

    /// <summary>Sends Cmd+key (macOS) or Ctrl+key to the focused app. If the user still holds the modifier, only the key is sent.</summary>
    public void SendModChord(KeyCode key)
    {
        var mod = Os.IsMac ? KeyCode.VcLeftMeta : KeyCode.VcLeftControl;
        bool held;
        lock (_gate) held = _modDown;
        if (!held) _sim.SimulateKeyPress(mod);
        _sim.SimulateKeyPress(key);
        _sim.SimulateKeyRelease(key);
        if (!held) _sim.SimulateKeyRelease(mod);
    }

    public void SendCopy() => SendModChord(KeyCode.VcC);
    public void SendPaste() => SendModChord(KeyCode.VcV);

    public void Dispose()
    {
        try { _hook.Dispose(); } catch { }
    }
}
