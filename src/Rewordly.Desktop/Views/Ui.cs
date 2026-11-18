using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

    public static Button Btn(string text, Action onClick, bool primary = false, string? icon = null, string hue = "Blue")
    {
        var b = MakeBtn(text, primary, icon, hue);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Button Btn(string text, Func<Task> onClick, bool primary = false, string? icon = null, string hue = "Blue")
    {
        var b = MakeBtn(text, primary, icon, hue);
        b.Click += async (_, _) =>
        {
            try { await onClick(); }
            catch (Exception ex) { Log.Error("ui", ex); await Dialog.InfoAsync("Rewordly", ex.Message); }
        };
        return b;
    }

    private static Button MakeBtn(string text, bool primary, string? icon, string hue)
    {
        var b = new Button { Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(icon != null ? 10 : 14, 6, 14, 6) };
        if (primary) b.Classes.Add("accent");
        b.Content = BtnContent(text, icon, primary ? null : hue);
        if (primary) InkOnHighlighter(b);
        return b;
    }

    /// <summary>Main-action buttons carry ink text on yellow while enabled; disabled falls back to Fluent's grey.</summary>
    public static void InkOnHighlighter(Button b)
    {
        var ink = new SolidColorBrush(Color.Parse(Theme.Ink));
        void Sync()
        {
            if (b.IsEffectivelyEnabled) b.SetValue(TextElement.ForegroundProperty, ink);
            else b.ClearValue(TextElement.ForegroundProperty);
            if (b.Content is string str) b.Content = new TextBlock { Text = str };
            foreach (var c in b.Content is Panel p ? p.Children.ToArray() : new[] { b.Content as Control })
                if (c is TextBlock t) { if (b.IsEffectivelyEnabled) t.Foreground = ink; else t.ClearValue(TextBlock.ForegroundProperty); }
                else if (c is PathIcon pi) { if (b.IsEffectivelyEnabled) pi.Foreground = ink; else pi.ClearValue(PathIcon.ForegroundProperty); }
        }
        b.PropertyChanged += (_, e) => { if (e.Property == InputElement.IsEffectivelyEnabledProperty || e.Property == ContentControl.ContentProperty) Sync(); };
        Sync();
    }

    /// <summary>Icon + label for any button-like control. A null hue makes the icon follow the text colour.</summary>
    public static object BtnContent(string text, string? icon, string? hue = "Blue")
    {
        if (icon == null) return text;
        return new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 7,
            Children = { Glyph(icon, 16, hue), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    /// <summary>Change a button's text and keep its icon.</summary>
    public static void SetLabel(ContentControl b, string text)
    {
        if (b.Content is StackPanel sp && sp.Children.OfType<TextBlock>().FirstOrDefault() is { } t) t.Text = text;
        else b.Content = text;
    }

    /// <summary>A line icon (Fluent UI System Icons). Hue is a key from Theme.Hues; null follows the text colour.</summary>
    public static PathIcon Glyph(string name, double size = 16, string? hue = "Blue")
    {
        var p = new PathIcon { Data = StreamGeometry.Parse(Icons.Paths[name]), Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        if (hue != null) p.Bind(PathIcon.ForegroundProperty, p.GetResourceObservable("Hue." + hue));
        return p;
    }

    /// <summary>Icon on a soft tinted square, used next to section headings.</summary>
    public static Border Chip(string icon, string hue, double size = 28)
    {
        var b = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.28),
            Child = Glyph(icon, size * 0.6, hue), VerticalAlignment = VerticalAlignment.Center,
        };
        b.Bind(Border.BackgroundProperty, b.GetResourceObservable("Chip." + hue));
        return b;
    }

    /// <summary>The app icon as an image.</summary>
    public static Image Logo(double size) => new()
    {
        Source = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(IconPng)), Width = size, Height = size,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>
    /// Top of every window: an ink band with the window's mark on a highlighter tile, title and one line
    /// of context, drawn marker strokes fading in on the right, and the marker-set rule underneath.
    /// Full and compact are the only two sizes.
    /// </summary>
    public static Control Header(string title, string subtitle, string? icon = null, bool compact = false)
    {
        double mark = compact ? 34 : 42;
        Control lead = icon == null
            ? Logo(mark)
            : new Border
            {
                Width = mark, Height = mark, CornerRadius = new CornerRadius(compact ? 9 : 11),
                Background = Theme.Highlighter(),
                Child = new PathIcon { Data = StreamGeometry.Parse(Icons.Paths[icon]), Width = compact ? 18 : 22, Height = compact ? 18 : 22, Foreground = new SolidColorBrush(Color.Parse(Theme.Ink)) },
            };
        var sub = new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = new SolidColorBrush(Color.Parse("#C9CBD3")), TextWrapping = TextWrapping.Wrap, IsVisible = subtitle.Length > 0 };
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = title, FontSize = compact ? 16 : 20, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                sub,
            },
        };
        var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*"), Margin = compact ? new Thickness(18, 11, 18, 11) : new Thickness(22, 18, 22, 18) };
        Grid.SetColumn(text, 1);
        row.Children.Add(lead);
        row.Children.Add(text);
        var strokes = new Image
        {
            Source = Theme.HeaderStrokes, Stretch = Stretch.UniformToFill, Width = 380,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Opacity = compact ? 0.6 : 0.85,
        };
        var band = new Panel { Background = Theme.InkBand(), ClipToBounds = true, Children = { strokes, row } };
        var rule = new Border { Height = 3, Background = Theme.Markers() };
        return new StackPanel { Children = { band, rule } };
    }

    /// <summary>
    /// Illustrated header for the welcome guide: a page being rewritten and highlighted on the right,
    /// the welcome on the left.
    /// </summary>
    public static Control Hero(string title, string subtitle)
    {
        var img = new Image
        {
            Source = Theme.Asset("hero.png"), Stretch = Stretch.UniformToFill, Height = 200,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
        };
        var text = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
            Width = 300, Margin = new Thickness(28, 0, 0, 0), Spacing = 8,
            Children =
            {
                Logo(44),
                new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = subtitle, FontSize = 13.5, Foreground = new SolidColorBrush(Color.Parse("#C9CBD3")), TextWrapping = TextWrapping.Wrap },
            },
        };
        ((Image)text.Children[0]).HorizontalAlignment = HorizontalAlignment.Left;
        var rule = new Border { Height = 3, Background = Theme.Markers() };
        return new StackPanel { Children = { new Panel { Height = 200, ClipToBounds = true, Background = Theme.InkBand(), Children = { img, text } }, rule } };
    }

    /// <summary>Header band on top, window content below.</summary>
    public static DockPanel Framed(Control header, Control body)
    {
        var d = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        d.Children.Add(header);
        d.Children.Add(body);
        return d;
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

    /// <summary>Group heading inside a window: more space above than below so groups read as separate.</summary>
    public static Control Section(string text, string? icon = null, string hue = "Blue")
    {
        var t = new TextBlock { Text = text, FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        if (icon == null) { t.Margin = new Thickness(0, 18, 0, 2); return t; }
        return new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 18, 0, 4),
            Children = { Chip(icon, hue, 28), t },
        };
    }

    /// <summary>Tab header with an icon.</summary>
    public static object TabHeader(string text, string icon, string hue) => new StackPanel
    {
        Orientation = Orientation.Horizontal, Spacing = 8,
        Children = { Glyph(icon, 18, hue), new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } },
    };

    /// <summary>Label sitting directly above the control it names.</summary>
    public static TextBlock Label(string text) => new()
    {
        Text = text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 4),
    };

    /// <summary>Short helper text under a control.</summary>
    public static TextBlock Hint(string text) => new()
    {
        Text = text, FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 2),
    };

    /// <summary>Label on the left, content on the right (used for a heading with a status next to it).</summary>
    public static Grid Split(Control left, Control right)
    {
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto") };
        Grid.SetColumn(right, 1);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        right.VerticalAlignment = VerticalAlignment.Bottom;
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }

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

    /// <summary>White (light) or raised (dark) surface with a hairline border.</summary>
    public static Border Card(Control child, double padding = 14)
    {
        var b = new Border { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        b.Bind(Border.BackgroundProperty, b.GetResourceObservable("Rw.CardBackground"));
        b.Bind(Border.BorderBrushProperty, b.GetResourceObservable("Rw.CardBorder"));
        return b;
    }
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

    /// <summary>Message with an icon chip on its left.</summary>
    private static Control Message(string message, string icon, string hue)
    {
        var chip = Ui.Chip(icon, hue, 36);
        chip.VerticalAlignment = VerticalAlignment.Top;
        chip.Margin = new Thickness(0, 2, 12, 0);
        var g = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };
        var t = Ui.Text(message);
        Grid.SetColumn(t, 1);
        g.Children.Add(chip);
        g.Children.Add(t);
        return g;
    }

    public static Task InfoAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource();
        Window? w = null;
        var ok = Ui.Btn("OK", () => w!.Close(), primary: true);
        ok.IsDefault = true; ok.IsCancel = true;
        var panel = new StackPanel { Spacing = 10, Children = { Message(message, "Info", "Blue"), new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok } } } };
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
        var panel = new StackPanel { Spacing = 10, Children = { Message(message, "Warning", "Yellow"), new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { y, n } } } };
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
        var panel = new StackPanel { Spacing = 10, Children = { Message(prompt, "Edit", "Pink"), box, new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } } } };
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
        Title = "Rewordly";
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        ShowActivated = false;
        Width = 360;
        SizeToContent = SizeToContent.Height;
        Control lead = warning ? Ui.Chip("Warning", "Yellow", 32) : Ui.Logo(32);
        lead.VerticalAlignment = VerticalAlignment.Top;
        lead.Margin = new Thickness(0, 1, 12, 0);
        var body = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = warning ? "Rewordly – attention" : "Rewordly", FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 },
            },
        };
        if (warning) ((TextBlock)body.Children[0]).Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Hue.Yellow"));
        var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };
        Grid.SetColumn(body, 1);
        row.Children.Add(lead);
        row.Children.Add(body);
        var card = new Border { Padding = new Thickness(14, 12), BorderThickness = new Thickness(1), Child = row };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("Rw.CardBackground"));
        card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("Rw.CardBorder"));
        Content = card;
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
        Title = "Rewordly";
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
        Title = "Rewordly";
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
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*,Auto"), Margin = new Thickness(14, 10, 14, 8) };
        var chip = Ui.Logo(34);
        chip.Margin = new Thickness(0, 0, 12, 0);
        var left = new StackPanel { Children = { Ui.Text(action + "…", bold: true), sub } };
        left.Children[0].Margin = new Thickness(0, 0, 8, 0);
        sub.Margin = new Thickness(0, 0, 8, 0);
        sub.FontSize = 12;
        Grid.SetColumn(left, 1);
        Grid.SetColumn(cancel, 2);
        grid.Children.Add(chip);
        grid.Children.Add(left);
        grid.Children.Add(cancel);
        var bar = new ProgressBar { IsIndeterminate = true, Height = 3, MinHeight = 3, CornerRadius = new CornerRadius(0), Margin = new Thickness(0) };
        Content = Ui.Rows("Auto,Auto", grid, bar);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) onCancel(); };
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => sub.Text = $"{model}  ·  {(DateTime.Now - start).TotalSeconds:0}s");
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Ui.PlaceNearPointer(this, 340, 70, dx: 14, dy: 14);
    }
}
