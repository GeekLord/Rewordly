using System.IO.Pipes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Rewordly.Core;
using Rewordly.Desktop.Platform;
using Rewordly.Desktop.Views;

namespace Rewordly.Desktop;

/// <summary>Owns settings, shortcuts, the tray icon and the action flow. Identical on every OS.</summary>
internal sealed class AppController
{
    public static AppController Instance { get; private set; } = null!;

    public SettingsStore Store { get; }
    public SecretStore Secrets { get; }
    public AppSettings Settings { get; private set; }
    public HttpClient Http { get; } = new() { Timeout = Timeout.InfiniteTimeSpan };
    public GlobalInput? Input { get; private set; }
    public ClipboardService Clipboard { get; }
    public ITts Tts { get; } = Platform.Tts.Create();

    private readonly IClassicDesktopStyleApplicationLifetime _life;
    private readonly MenuHost _host = new();
    private TrayIcon? _tray;
    private bool _busy;
    private CancellationTokenSource? _cts;
    private SettingsWindow? _settingsWindow;

    public AppController(IClassicDesktopStyleApplicationLifetime life, bool background)
    {
        Instance = this;
        _life = life;
        var folder = SettingsStore.DefaultFolder;
        Log.Folder = folder;
        Secrets = new SecretStore(folder);
        Store = new SettingsStore(folder, Secrets);
        Settings = Store.Load();
        Clipboard = new ClipboardService(() => TopLevel.GetTopLevel(_host)?.Clipboard);

        ApplyTheme();
        CreateTray();
        StartInput();
        StartInstancePipe();

        var first = !File.Exists(Store.FilePath);
        if (first) Store.Save(Settings);

        Dispatcher.UIThread.Post(async () =>
        {
            if (Os.IsMac && !GlobalInput.HasAccessibility(prompt: true)) await ExplainMacPermissionsAsync();
            if (Os.IsWayland)
                Toast.Show("You're on a Wayland session. System-wide shortcuts only work in X11/XWayland apps. Use an X11 session for full support.", warning: true);

            if (first || NeedsKey()) { ShowGuide(); ShowSettings(); }
            else if (!background) Toast.Show($"Ready. Select text and press {Os.ModLabel}+C twice (hold {Os.ModLabel}). Model: {Display(Settings.Active.Model)}");
        });
    }

    private bool NeedsKey()
    {
        var p = ProviderPresets.Get(Settings.ActiveProvider);
        return p.NeedsKey && string.IsNullOrEmpty(Store.GetApiKey(Settings, p.Id));
    }

    public bool Dark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public Screen? ScreenAt(int x, int y)
    {
        try { return _host.Screens.ScreenFromPoint(new PixelPoint(x, y)) ?? _host.Screens.Primary; }
        catch { return null; }
    }

    // ------------------------------------------------------------------ settings
    public void SaveSettings()
    {
        try { Store.Save(Settings); }
        catch (Exception ex) { Log.Error("save", ex); Toast.Show("Could not save settings: " + ex.Message, warning: true); }
    }

    public void ApplySettings(AppSettings updated)
    {
        Settings = updated;
        Settings.Normalize();
        SaveSettings();
        try { AutoStart.Apply(Settings.StartWithWindows); } catch (Exception ex) { Log.Error("autostart", ex); Toast.Show("Could not change start-at-login: " + ex.Message, true); }
        ApplyTheme();
        ApplyInputSettings();
        RebuildTrayMenu();
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = Settings.Theme switch { "dark" => ThemeVariant.Dark, "light" => ThemeVariant.Light, _ => ThemeVariant.Default };
    }

    private void StartInput()
    {
        try
        {
            Input = new GlobalInput();
            Input.DoubleCopy += () => Dispatcher.UIThread.Post(() => _ = OnTriggerAsync(alt: false));
            Input.AltShortcut += () => Dispatcher.UIThread.Post(() => _ = OnTriggerAsync(alt: true));
            Input.Failed += ex => Dispatcher.UIThread.Post(() =>
            {
                Log.Error("hook", ex);
                Toast.Show("System-wide shortcuts could not start: " + ex.Message, warning: true);
            });
            ApplyInputSettings();
            Input.Start();
        }
        catch (Exception ex)
        {
            Log.Error("hook.start", ex);
            Toast.Show("System-wide shortcuts are unavailable: " + ex.Message, warning: true);
        }
    }

    private void ApplyInputSettings()
    {
        if (Input == null) return;
        Input.WindowMs = Settings.DoubleCopyWindowMs;
        Input.DoubleCopyEnabled = Settings.TriggerDoubleCtrlC;
        Input.AltShortcutEnabled = Settings.TriggerAltShortcut;
        Input.Paused = Settings.Paused;
    }

    private static async Task ExplainMacPermissionsAsync()
    {
        var open = await Dialog.AskAsync("Rewordly needs permission",
            "To notice Cmd+C pressed twice and to paste results, macOS needs you to allow Rewordly under " +
            "System Settings > Privacy & Security > Accessibility (and Input Monitoring if listed). " +
            "Turn Rewordly on there, then quit and reopen Rewordly.", "Open System Settings", "Later");
        if (open) Os.Open("x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility");
    }

    // ------------------------------------------------------------------ tray
    private void CreateTray()
    {
        try
        {
            _tray = new TrayIcon { Icon = Ui.Icon, ToolTipText = "Rewordly", IsVisible = true };
            _tray.Clicked += (_, _) => ShowSettings();
            TrayIcon.SetIcons(Application.Current!, new TrayIcons { _tray });
            RebuildTrayMenu();
        }
        catch (Exception ex) { Log.Error("tray", ex); }
    }

    public void RebuildTrayMenu()
    {
        if (_tray == null) return;
        var m = new NativeMenu();
        m.Add(new NativeMenuItem($"Rewordly · {ProviderPresets.Get(Settings.ActiveProvider).Name} · {Display(Settings.Active.Model)}") { IsEnabled = false });
        m.Add(new NativeMenuItemSeparator());
        m.Add(MenuFactory.ModelNativeMenu(this));
        m.Add(Item("Settings…", ShowSettings));
        m.Add(Item("Chat…", () => new ChatWindow("", WindowFocus.Target.None).Show()));
        m.Add(Item("Quick guide", ShowGuide));
        var pause = new NativeMenuItem("Pause shortcuts") { ToggleType = NativeMenuItemToggleType.CheckBox, IsChecked = Settings.Paused };
        pause.Click += (_, _) => { Settings.Paused = !Settings.Paused; SaveSettings(); ApplyInputSettings(); RebuildTrayMenu(); Toast.Show(Settings.Paused ? "Shortcuts paused." : "Shortcuts active."); };
        m.Add(pause);
        m.Add(Item("Open data folder", () => { Directory.CreateDirectory(Store.Folder); Os.Open(Store.Folder); }));
        m.Add(new NativeMenuItemSeparator());
        m.Add(Item("Quit Rewordly", Quit));
        _tray.Menu = m;
        var tip = $"Rewordly – {Display(Settings.Active.Model)}";
        _tray.ToolTipText = tip.Length > 60 ? tip[..60] : tip;
    }

    private static NativeMenuItem Item(string text, Action a)
    {
        var i = new NativeMenuItem(text);
        i.Click += (_, _) => a();
        return i;
    }

    public static string Display(string model) => string.IsNullOrWhiteSpace(model) ? "(no model)" : model;

    public void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true }) { Ui.ShowFocused(_settingsWindow); return; }
        _settingsWindow = new SettingsWindow(this);
        Ui.ShowFocused(_settingsWindow);
    }

    public void ShowGuide() => Ui.ShowFocused(new GuideWindow(this));

    public void Quit()
    {
        _cts?.Cancel();
        Input?.Dispose();
        if (_tray != null) _tray.IsVisible = false;
        _life.Shutdown();
    }

    // ------------------------------------------------------------------ single instance
    public static string PipeName => "Rewordly-" + Environment.UserName;

    private void StartInstancePipe()
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1);
                    server.WaitForConnection();
                    using var r = new StreamReader(server);
                    var cmd = r.ReadLine();
                    Dispatcher.UIThread.Post(cmd == "chat" ? () => new ChatWindow("", WindowFocus.Target.None).Show() : ShowSettings);
                }
                catch (Exception ex) { Log.Error("pipe", ex); Thread.Sleep(1000); }
            }
        }) { IsBackground = true, Name = "rewordly-pipe" };
        t.Start();
    }

    // ------------------------------------------------------------------ triggers
    private async Task OnTriggerAsync(bool alt)
    {
        if (_busy) return;
        var target = WindowFocus.Capture();
        if (WindowFocus.IsOwn(target)) return;
        try
        {
            var text = await CaptureSelectionAsync(alt);
            Begin(text, target);
        }
        catch (Exception ex) { Log.Error("trigger", ex); Toast.Show(ex.Message, true); }
    }

    /// <summary>
    /// Copies the current selection into memory: clear the clipboard, send Copy to the app in front,
    /// wait for text, then put the previous clipboard back (if enabled).
    /// </summary>
    private async Task<string> CaptureSelectionAsync(bool alt)
    {
        if (alt && Os.IsLinux)
        {
            var primary = await Task.Run(ClipboardService.ReadPrimarySelection);
            if (primary != null) return primary;
        }
        var restore = Settings.RestoreClipboard;
        ClipboardService.Snapshot? snap = null;
        if (alt && restore) snap = await Clipboard.SnapshotAsync();
        if (alt && Os.IsMac) await Task.Delay(250); // let Ctrl/Cmd from the shortcut settle

        // For the double copy, the user's own presses already copied the selection; copy again after clearing
        // so stale clipboard content is never mistaken for a selection.
        await Task.Delay(alt ? 0 : 120);
        await Clipboard.ClearAsync();
        await Task.Delay(30);
        Input?.SendCopy();
        var text = await Clipboard.WaitForTextAsync(1500);
        if (alt && restore) await Clipboard.RestoreAsync(snap);
        return text;
    }

    private void Begin(string text, WindowFocus.Target target)
    {
        if (string.IsNullOrWhiteSpace(text)) { Toast.Show("No text selected. Select some text first.", warning: true); return; }
        int words = TextUtil.CountWords(text);
        if (words > Settings.MaxWords)
        {
            Toast.Show($"The selection has {words} words; the limit is {Settings.MaxWords}. You can raise it in Settings.", warning: true);
            return;
        }
        var menu = MenuFactory.Build(this, text, target);
        var (x, y) = Input?.Pointer ?? (200, 200);
        _host.Open(menu, new PixelPoint(x, y));
    }

    // ------------------------------------------------------------------ actions
    public async void Run(ActionSpec action, string text, WindowFocus.Target target)
    {
        try
        {
            switch (action.Kind)
            {
                case ActionKind.Chat: Ui.ShowFocused(new ChatWindow(text, target)); return;
                case ActionKind.Listen: Ui.ShowFocused(new SpeechWindow(text)); return;
            }
            if (action.Kind == ActionKind.Style) AppSettings.PushRecent(Settings.RecentStyles, action.Argument, AppSettings.MaxRecentStyles);
            if (action.Kind == ActionKind.Translate) AppSettings.PushRecent(Settings.RecentLanguages, action.Argument, AppSettings.MaxRecentLanguages);
            SaveSettings();

            var prompt = Prompts.Build(Settings, action, text);
            var result = await RunWithBusyAsync(prompt, action.Title);
            if (result == null) return;
            Ui.ShowFocused(new PreviewWindow(action, text, TextUtil.CleanModelOutput(result.Text, text), prompt, target, result));
        }
        catch (Exception ex) { Log.Error("run", ex); await Dialog.InfoAsync("Rewordly", ex.Message); }
    }

    /// <summary>Calls the AI with a small working popup. Returns null if cancelled or failed (errors are shown).</summary>
    public async Task<AiResult?> RunWithBusyAsync(PromptPair prompt, string title, string? modelOverride = null)
    {
        if (_busy) return null;
        _busy = true;
        _cts = new CancellationTokenSource();
        var cts = _cts;
        var busy = new BusyWindow(title, Display(modelOverride ?? Settings.Active.Model), () => cts.Cancel());
        busy.Show();
        try { return await CompleteAsync(prompt, cts.Token, modelOverride); }
        catch (OperationCanceledException) { return null; }
        catch (AiException ex) { busy.Close(); await Dialog.InfoAsync("Rewordly", ex.Message); return null; }
        catch (Exception ex) { Log.Error("ai", ex); busy.Close(); await Dialog.InfoAsync("Rewordly", ex.Message); return null; }
        finally { busy.Close(); _busy = false; }
    }

    public Task<AiResult> CompleteAsync(PromptPair prompt, CancellationToken ct, string? modelOverride = null)
    {
        var opts = AiClientFactory.FromSettings(Settings, Store, modelOverride: modelOverride);
        return AiClientFactory.Create(Http, opts).CompleteAsync(prompt, ct);
    }

    /// <summary>Gives focus back to <paramref name="target"/> and pastes <paramref name="text"/> there.</summary>
    public async Task PasteIntoAsync(WindowFocus.Target target, string text, Window? hideFirst)
    {
        var snap = Settings.RestoreClipboard ? await Clipboard.SnapshotAsync() : null;
        await Clipboard.SetTextAsync(text);
        hideFirst?.Hide();
        await Task.Delay(Os.IsMac ? 200 : 120);
        WindowFocus.Restore(target);
        await Task.Delay(Os.IsMac ? 250 : 150);
        Input?.SendPaste();
        await Task.Delay(500); // let the app read the clipboard before restoring it
        if (Settings.RestoreClipboard) await Clipboard.RestoreAsync(snap);
    }

    public void SelectModel(string providerId, string model)
    {
        Settings.ActiveProvider = providerId;
        var cfg = Settings.GetProvider(providerId);
        cfg.Model = model;
        AppSettings.PushRecent(cfg.RecentModels, model, AppSettings.MaxRecentModels);
        SaveSettings();
        RebuildTrayMenu();
        Toast.Show($"Now using {ProviderPresets.Get(providerId).Name} · {model}");
    }
}
