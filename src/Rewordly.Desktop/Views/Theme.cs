using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Rewordly.Desktop.Views;

/// <summary>
/// Rewordly's visual world: highlighter on paper. Surfaces are plain paper (light) or ink (dark); colour
/// comes only from marker inks. Highlighter yellow is the one main action; the other inks (pink, violet,
/// blue, orange, green) mark the kind of action, always together with an icon.
/// Every ink passes 4.5:1 against the ground in both themes; ink-on-yellow is 11:1 or better.
/// </summary>
internal static class Theme
{
    // ------------------------------------------------------------------ neutral ramp (paper → ink)
    public const string Ground = "#F8F8F6";     // light ground
    public const string Paper = "#FFFFFF";      // light raised surface
    public const string Rule = "#E4E3DE";       // light hairline
    public const string Ink = "#15171C";        // light text, dark header
    public const string Night = "#121317";      // dark ground
    public const string NightRaised = "#1A1C22";
    public const string NightRule = "#2B2E37";
    public const string Chalk = "#EDECE8";      // dark text

    /// <summary>Marker inks per kind of action: (light theme, dark theme), both text-safe.</summary>
    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Hues = new Dictionary<string, (string, string)>
    {
        ["Blue"] = ("#1D64C2", "#7DB6FF"),    // the app itself, settings, chat
        ["Pink"] = ("#C2185B", "#FF7FB4"),    // rewriting and styles
        ["Violet"] = ("#6A3FD0", "#B9A2FF"),  // summary, explain, models
        ["Orange"] = ("#B24A0B", "#FF9F63"),  // translate, listen
        ["Green"] = ("#1A7A43", "#5EDB8F"),   // proofreading, success
        ["Yellow"] = ("#8A6200", "#FFD84A"),  // warnings, keys
        ["Muted"] = ("#64666E", "#A3A6B0"),   // neutral
    };

    /// <summary>The bright highlighter ink itself, used for washes behind icons.</summary>
    public static readonly IReadOnlyDictionary<string, string> Wash = new Dictionary<string, string>
    {
        ["Blue"] = "#4DA3FF", ["Pink"] = "#FF5FA2", ["Violet"] = "#9B7BFF", ["Orange"] = "#FF8A3D",
        ["Green"] = "#3DDC84", ["Yellow"] = "#FFD43B", ["Muted"] = "#9A9CA5",
    };

    public static Bitmap Asset(string name) => new(AssetLoader.Open(new Uri($"avares://Rewordly/Assets/Brand/{name}")));

    private static Bitmap? _strokes;
    public static Bitmap HeaderStrokes => _strokes ??= Asset("header-strokes.png");

    /// <summary>Ink band behind every header.</summary>
    public static LinearGradientBrush InkBand() => Gradient(0, "#15171C", "#1E2028", "#262833");

    /// <summary>The marker set in a row: the thin rule under every header and the progress bar.</summary>
    public static LinearGradientBrush Markers() => Gradient(0, "#FFD43B", "#FF8A3D", "#FF5FA2", "#9B7BFF", "#4DA3FF");

    /// <summary>Highlighter yellow for the main action and small solid marks.</summary>
    public static LinearGradientBrush Highlighter() => Gradient("#FFE066", "#FFC93C");
    private static LinearGradientBrush HighlighterHover() => Gradient("#FFE985", "#FFD24F");
    private static LinearGradientBrush HighlighterPressed() => Gradient("#F5CC2E", "#F0B82A");

    /// <summary>Horizontal gradient (angle 0) through the given stops.</summary>
    public static LinearGradientBrush Gradient(int horizontal, params string[] stops)
    {
        var b = Gradient(stops);
        b.EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative);
        b.StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        return b;
    }

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

    private static SolidColorBrush B(string c) => new(Color.Parse(c));

    public static void Apply(Application app)
    {
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Light] = new ColorPaletteResources
        {
            Accent = Color.Parse("#1D64C2"), RegionColor = Color.Parse(Ground), BaseHigh = Color.Parse(Ink),
        };
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = Color.Parse("#5E9FF5"), RegionColor = Color.Parse(Night), BaseHigh = Color.Parse(Chalk),
        };
        app.Styles.Add(fluent);

        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (name, (l, d)) in Hues)
        {
            light["Hue." + name] = B(l);
            dark["Hue." + name] = B(d);
            var w = Color.Parse(Wash[name]);
            light["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x38, w.R, w.G, w.B));
            dark["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x30, w.R, w.G, w.B));
        }
        light["Rw.CardBackground"] = B(Paper);
        dark["Rw.CardBackground"] = B(NightRaised);
        light["Rw.CardBorder"] = B(Rule);
        dark["Rw.CardBorder"] = B(NightRule);
        light["Rw.Ink"] = B(Ink);
        dark["Rw.Ink"] = B(Chalk);

        void Surface(ResourceDictionary d, string bg, string hover, string pressed, string border, string field, string flyout)
        {
            foreach (var k in new[] { "ButtonBackground", "ComboBoxBackground", "NumericUpDownBackground" }) d[k] = B(bg);
            foreach (var k in new[] { "ButtonBackgroundPointerOver", "ComboBoxBackgroundPointerOver" }) d[k] = B(hover);
            foreach (var k in new[] { "ButtonBackgroundPressed", "ComboBoxBackgroundPressed" }) d[k] = B(pressed);
            foreach (var k in new[] { "ButtonBorderBrush", "ButtonBorderBrushPointerOver", "ComboBoxBorderBrush" }) d[k] = B(border);
            d["TextControlBackground"] = B(field);
            d["TextControlBackgroundFocused"] = B(field);
            d["TextControlBorderBrush"] = B(border);
            d["MenuFlyoutPresenterBackground"] = B(flyout);
            d["ComboBoxDropDownBackground"] = B(flyout);
            d["MenuFlyoutPresenterBorderBrush"] = B(border);
        }
        Surface(light, Paper, "#F2F2EE", "#E9E9E4", "#D9D8D2", Paper, Paper);
        Surface(dark, "#22252D", "#2A2E37", "#1A1C22", "#363A46", "#0D0E11", "#1C1E25");
        foreach (var d in new[] { light, dark })
            foreach (var k in new[] { "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed" })
                d[k] = B(Ink);
        app.Resources["ButtonBorderThemeThickness"] = new Thickness(1);
        app.Resources.ThemeDictionaries[ThemeVariant.Light] = light;
        app.Resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        app.Resources["ControlCornerRadius"] = new CornerRadius(8);
        app.Resources["OverlayCornerRadius"] = new CornerRadius(12);

        // The one main action per window: highlighter yellow with ink text, the same in both themes.
        var ink = B(Ink);
        Style Accent(Func<Selector?, Selector> state, IBrush bg) => new(x => state(x.OfType<Button>().Class("accent"))
            .Not(y => y.Class(":disabled")).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, bg),
                new Setter(ContentPresenter.ForegroundProperty, ink),
                new Setter(ContentPresenter.BorderBrushProperty, B("#E0AE1F")),
            },
        };
        app.Styles.Add(Accent(x => x!, Highlighter()));
        app.Styles.Add(Accent(x => x!.Class(":pointerover"), HighlighterHover()));
        app.Styles.Add(Accent(x => x!.Class(":pressed"), HighlighterPressed()));

        app.Styles.Add(new Style(x => x.OfType<ProgressBar>())
        {
            Setters = { new Setter(ProgressBar.ForegroundProperty, Markers()) },
        });
    }
}
