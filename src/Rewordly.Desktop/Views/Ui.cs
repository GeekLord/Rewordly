using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Code-only UI helpers so every window looks the same on all three systems.</summary>
internal static class Ui
{
    private static WindowIcon? _icon;
    private static byte[]? _png;

    public static byte[] IconPng
    {
        get
        {
            if (_png != null) return _png;
            using var s = typeof(Ui).Assembly.GetManifestResourceStream("Rewordly.icon.png")!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return _png = ms.ToArray();
        }
    }

    public static WindowIcon Icon => _icon ??= new WindowIcon(new MemoryStream(IconPng));

    public static Button Btn(string text, Action onClick, bool primary = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 5) };
        if (primary) b.Classes.Add("accent");
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Button Btn(string text, Func<Task> onClick, bool primary = false)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 5) };
        if (primary) b.Classes.Add("accent");
        b.Click += async (_, _) =>
        {
            try { await onClick(); }
            catch (Exception ex) { Log.Error("ui", ex); await Dialog.InfoAsync("Rewordly", ex.Message); }
        };
        return b;
    }

    public static TextBlock Text(string text, bool muted = false, bool bold = false, double size = 0) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Opacity = muted ? 0.7 : 1,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        FontSize = size > 0 ? size : 13,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 4, 8, 4),
    };

    public static TextBox Area(string text, bool readOnly) => new()
    {
        Text = text, IsReadOnly = readOnly, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.Wrap,
        FontSize = 14, MinHeight = 60,
        VerticalContentAlignment = VerticalAlignment.Top,
    };

    public static WrapPanel Row(params Control[] items)
    {
        var p = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var i in items) p.Children.Add(i);
        return p;
    }

    public static Grid Rows(string spec, params Control[] items)
    {
        var g = new Grid { RowDefinitions = RowDefinitions.Parse(spec) };
        for (int i = 0; i < items.Length; i++) { Grid.SetRow(items[i], i); g.Children.Add(items[i]); }
        return g;
    }

    /// <summary>Cmd on macOS, Ctrl elsewhere.</summary>
    public static bool Mod(KeyEventArgs e) => Os.IsMac ? e.KeyModifiers.HasFlag(KeyModifiers.Meta) : e.KeyModifiers.HasFlag(KeyModifiers.Control);

    public static void Prepare(Window w, string title, double width, double height)
    {
        w.Title = title;
        w.Icon = Icon;
        w.Width = width;
        w.Height = height;
        w.MinWidth = Math.Min(width, 420);
        w.MinHeight = Math.Min(height, 280);
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        PlaceNearPointer(w, width, height);
    }

    public static void PlaceNearPointer(Window w, double width, double height, int dx = -1, int dy = -40)
    {
        var app = AppController.Instance;
        var (x, y) = app.Input?.Pointer ?? (200, 200);
        var screen = app.ScreenAt(x, y);
        double scale = screen?.Scaling ?? 1;
        if (Os.IsMac) scale = 1; // macOS positions are in points
        int pw = (int)(width * scale), ph = (int)(height * scale);
        int px = dx < 0 ? x - pw / 2 : x + dx;
        int py = y + dy;
        if (screen != null)
        {
            var wa = screen.WorkingArea;
            px = Math.Clamp(px, wa.X, Math.Max(wa.X, wa.Right - pw));
            py = Math.Clamp(py, wa.Y, Math.Max(wa.Y, wa.Bottom - ph));
        }
        w.Position = new PixelPoint(px, py);
    }

    /// <summary>Show and focus a window even if another app is in front.</summary>
    public static void ShowFocused(Window w)
    {
        w.Show();
        w.Activate();
        if (Os.IsWindows) WindowFocus.ForceForeground(w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero);
        Dispatcher.UIThread.Post(() => w.Activate(), DispatcherPriority.Background);
    }

    public static Border Card(Control child) => new()
    {
        Child = child, Padding = new Thickness(2), CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)),
    };
}

/// <summary>Simple modal-style dialogs that work without an owner window.</summary>
internal static class Dialog
{
    private static Window Make(string title, Control body, double width = 440)
    {
        var w = new Window
        {
            Title = title, Icon = Ui.Icon, Width = width, SizeToContent = SizeToContent.Height, CanResize = false,
            Topmost = true, WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowInTaskbar = true,
            Content = new Border { Padding = new Thickness(18), Child = body },
        };
        return w;
    }

    public static Task InfoAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();
        Window? w = null;
        var ok = Ui.Btn("OK", () => w!.Close(), primary: true);
        ok.IsDefault = true; ok.IsCancel = true;
        var panel = new StackPanel { Spacing = 10, Children = { Ui.Text(message), new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok } } } };
        w = Make(title, panel);
        w.Closed += (_, _) => tcs.TrySetResult();
        Ui.ShowFocused(w);
        return tcs.Task;
    }

    public static Task<bool> AskAsync(string title, string message, string yes = "Yes", string no = "No")
    {
        var tcs = new TaskCompletionSource<bool>();
        Window? w = null;
        bool result = false;
        var y = Ui.Btn(yes, () => { result = true; w!.Close(); }, primary: true);
        var n = Ui.Btn(no, () => w!.Close());
        y.IsDefault = true; n.IsCancel = true;
        var panel = new StackPanel { Spacing = 10, Children = { Ui.Text(message), new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { y, n } } } };
        w = Make(title, panel);
        w.Closed += (_, _) => tcs.TrySetResult(result);
        Ui.ShowFocused(w);
        return tcs.Task;
    }

    public static Task<string?> InputAsync(string title, string prompt, int maxLength, string initial = "")
    {
        var tcs = new TaskCompletionSource<string?>();
        Window? w = null;
        string? result = null;
        var box = new TextBox { Text = initial, MaxLength = maxLength, FontSize = 14 };
        var ok = Ui.Btn("OK", () => { result = box.Text?.Trim(); w!.Close(); }, primary: true);
        var cancel = Ui.Btn("Cancel", () => w!.Close());
        ok.IsDefault = true; cancel.IsCancel = true;
        var panel = new StackPanel { Spacing = 10, Children = { Ui.Text(prompt), box, new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } } } };
        w = Make(title, panel, 480);
        w.Opened += (_, _) => box.Focus();
        w.Closed += (_, _) => tcs.TrySetResult(string.IsNullOrWhiteSpace(result) ? null : result);
        Ui.ShowFocused(w);
        return tcs.Task;
    }
}

/// <summary>Small notification in the corner of the screen (works even where tray balloons don't exist).</summary>
internal sealed class Toast : Window
{
    private static Toast? _current;

    private Toast(string message, bool warning)
    {
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        ShowActivated = false;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        Content = new Border
        {
            Padding = new Thickness(14, 10),
            BorderThickness = new Thickness(0, 0, 0, 0),
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = warning ? "Rewordly – attention" : "Rewordly", FontWeight = FontWeight.SemiBold, Foreground = warning ? Brushes.OrangeRed : null },
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
        PointerPressed += (_, _) => Close();
    }

    public static void Show(string message, bool warning = false)
    {
        _current?.Close();
        var t = _current = new Toast(message, warning);
        void Place()
        {
            var app = AppController.Instance;
            var (x, y) = app.Input?.Pointer ?? (0, 0);
            var screen = app.ScreenAt(x, y) ?? t.Screens.Primary;
            if (screen == null || t.Bounds.Height <= 0) return;
            var wa = screen.WorkingArea;
            double s = Os.IsMac ? 1 : screen.Scaling;
            t.Position = new PixelPoint(wa.Right - (int)((t.Bounds.Width + 16) * s), wa.Bottom - (int)((t.Bounds.Height + 16) * s));
        }
        t.Opacity = 0;
        t.Opened += (_, _) => Dispatcher.UIThread.Post(() => { Place(); t.Opacity = 1; }, DispatcherPriority.Background);
        t.Show();
        DispatcherTimer.RunOnce(() => { if (_current == t) t.Close(); }, TimeSpan.FromSeconds(warning ? 6 : 4));
    }
}

/// <summary>Tiny invisible window at the pointer that anchors the action menu.</summary>
internal sealed class MenuHost : Window
{
    private readonly Border _anchor = new() { Width = 1, Height = 1, Background = Brushes.Transparent };

    public MenuHost()
    {
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Width = 1; Height = 1;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Content = _anchor;
    }

    public void Open(ContextMenu menu, PixelPoint at)
    {
        Position = at;
        Ui.ShowFocused(this);
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
        menu.Closed += (_, _) => Dispatcher.UIThread.Post(Hide);
        Dispatcher.UIThread.Post(() => menu.Open(_anchor), DispatcherPriority.Background);
    }
}

/// <summary>Small always-on-top "working" popup with Cancel (Esc also cancels).</summary>
internal sealed class BusyWindow : Window
{
    private readonly DispatcherTimer _timer;

    public BusyWindow(string action, string model, Action onCancel)
    {
        SystemDecorations = SystemDecorations.BorderOnly;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Width = 340;
        SizeToContent = SizeToContent.Height;
        Icon = Ui.Icon;
        var start = DateTime.Now;
        var sub = Ui.Text(model, muted: true);
        var cancel = Ui.Btn("Cancel", onCancel);
        cancel.Margin = new Thickness(0);
        cancel.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"), Margin = new Thickness(14, 10) };
        var left = new StackPanel { Children = { Ui.Text(action + "…", bold: true), sub } };
        Grid.SetColumn(cancel, 1);
        grid.Children.Add(left);
        grid.Children.Add(cancel);
        Content = grid;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) onCancel(); };
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => sub.Text = $"{model}  ·  {(DateTime.Now - start).TotalSeconds:0}s");
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Ui.PlaceNearPointer(this, 340, 70, dx: 14, dy: 14);
    }
}
