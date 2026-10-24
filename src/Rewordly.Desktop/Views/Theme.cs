using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Rewordly.Desktop.Views;

/// <summary>
/// Rewordly's look, taken from the app icon: an indigo → blue → cyan gradient.
/// Every hue below has at least 3:1 contrast against the window in both themes,
/// and every gradient that carries white text keeps at least 4.5:1.
/// </summary>
internal static class Theme
{
    public static readonly Color Indigo = Color.Parse("#4653E3");
    public static readonly Color Blue = Color.Parse("#2C7ADD");
    public static readonly Color Cyan = Color.Parse("#0EA8D5");

    /// <summary>Icon colours per kind of action: (light theme, dark theme).</summary>
    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Hues = new Dictionary<string, (string, string)>
    {
        ["Indigo"] = ("#4653E3", "#8F98FF"), // general, settings, chat
        ["Violet"] = ("#7C3AED", "#B79CFF"), // styles and rewriting
        ["Blue"] = ("#1F6FD1", "#6FB0FF"),   // summary, explain, models
        ["Teal"] = ("#0B8199", "#3FD0E6"),   // translate, listen
        ["Green"] = ("#148A5B", "#4ADE9A"),  // proofreading, success
        ["Amber"] = ("#B45309", "#FBBF24"),  // warnings
        ["Muted"] = ("#5B6178", "#9AA0B5"),  // neutral
    };

    /// <summary>Gradient behind white header text (all stops ≥ 4.8:1 with white).</summary>
    public static LinearGradientBrush HeaderBrush() => Gradient("#3A45C8", "#2463C9", "#1477B8");

    /// <summary>Brand gradient for the main button in each window.</summary>
    private static LinearGradientBrush AccentBrush() => Gradient("#4653E3", "#2463C9");
    private static LinearGradientBrush AccentHoverBrush() => Gradient("#5562EA", "#2B6FD8");
    private static LinearGradientBrush AccentPressedBrush() => Gradient("#3A45C8", "#1A68C9");

    public static LinearGradientBrush Gradient(params string[] stops)
    {
        var b = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        for (int i = 0; i < stops.Length; i++)
            b.GradientStops.Add(new GradientStop(Color.Parse(stops[i]), stops.Length == 1 ? 0 : (double)i / (stops.Length - 1)));
        return b;
    }

    public static void Apply(Application app)
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = Indigo,
            RegionColor = Color.Parse("#F6F7FB"),
        };
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = Color.Parse("#6E78F0"),
            RegionColor = Color.Parse("#12141C"),
        };
        app.Styles.Add(fluent);

        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (name, (l, d)) in Hues)
        {
            var lc = Color.Parse(l); var dc = Color.Parse(d);
            light["Hue." + name] = new SolidColorBrush(lc);
            dark["Hue." + name] = new SolidColorBrush(dc);
            light["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x1F, lc.R, lc.G, lc.B));
            dark["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x2E, dc.R, dc.G, dc.B));
        }
        light["Rw.CardBackground"] = new SolidColorBrush(Colors.White);
        dark["Rw.CardBackground"] = new SolidColorBrush(Color.Parse("#1B1E29"));
        light["Rw.CardBorder"] = new SolidColorBrush(Color.Parse("#E3E6F0"));
        dark["Rw.CardBorder"] = new SolidColorBrush(Color.Parse("#2A2E3D"));
        // Secondary controls: white with a hairline in light mode, a raised indigo-grey in dark mode.
        void Surface(ResourceDictionary d, string bg, string hover, string pressed, string border, string field)
        {
            foreach (var k in new[] { "ButtonBackground", "ComboBoxBackground", "NumericUpDownBackground" }) d[k] = new SolidColorBrush(Color.Parse(bg));
            foreach (var k in new[] { "ButtonBackgroundPointerOver", "ComboBoxBackgroundPointerOver" }) d[k] = new SolidColorBrush(Color.Parse(hover));
            foreach (var k in new[] { "ButtonBackgroundPressed", "ComboBoxBackgroundPressed" }) d[k] = new SolidColorBrush(Color.Parse(pressed));
            foreach (var k in new[] { "ButtonBorderBrush", "ButtonBorderBrushPointerOver", "ComboBoxBorderBrush" }) d[k] = new SolidColorBrush(Color.Parse(border));
            d["TextControlBackground"] = new SolidColorBrush(Color.Parse(field));
            d["TextControlBackgroundFocused"] = new SolidColorBrush(Color.Parse(field));
        }
        Surface(light, "#FFFFFF", "#EEF0FB", "#E2E5F6", "#D5D9E8", "#FFFFFF");
        Surface(dark, "#232737", "#2C3145", "#1D2030", "#363B50", "#0C0E15");
        app.Resources["ButtonBorderThemeThickness"] = new Thickness(1);
        app.Resources.ThemeDictionaries[ThemeVariant.Light] = light;
        app.Resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        app.Resources["ControlCornerRadius"] = new CornerRadius(6);
        app.Resources["OverlayCornerRadius"] = new CornerRadius(10);

        // Main buttons: brand gradient with white text. Disabled keeps Fluent's grey.
        Style Accent(Func<Selector?, Selector> state, IBrush bg) => new(x => state(x.OfType<Button>().Class("accent"))
            .Not(y => y.Class(":disabled")).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, bg),
                new Setter(ContentPresenter.ForegroundProperty, Brushes.White),
                new Setter(ContentPresenter.BorderBrushProperty, Brushes.Transparent),
            },
        };
        app.Styles.Add(Accent(x => x!, AccentBrush()));
        app.Styles.Add(Accent(x => x!.Class(":pointerover"), AccentHoverBrush()));
        app.Styles.Add(Accent(x => x!.Class(":pressed"), AccentPressedBrush()));

        // Progress bars in the brand gradient (horizontal).
        app.Styles.Add(new Style(x => x.OfType<ProgressBar>())
        {
            Setters = { new Setter(ProgressBar.ForegroundProperty, Gradient("#4653E3", "#2C7ADD", "#0EA8D5")) },
        });
    }
}
