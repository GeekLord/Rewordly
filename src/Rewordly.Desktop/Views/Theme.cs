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
/// Rewordly's visual world: hand block-printed cotton. Indigo vat dye is the shell, madder red is the
/// one main action, and the other dyes (plum, peacock, leaf, turmeric) mark the kind of action.
/// Neutrals come from one ramp between unbleached cotton and indigo-black ink; nothing else is grey.
/// Every dye passes 4.5:1 against the ground in both themes; every surface carrying white text passes 4.5:1.
/// </summary>
internal static class Theme
{
    // ------------------------------------------------------------------ neutral ramp (cotton → ink)
    public const string Cotton = "#FBF8F2";     // light ground
    public const string Paper = "#FFFFFF";      // light raised surface
    public const string Thread = "#E7E1D4";     // light hairline
    public const string Ink = "#1B1E33";        // light text
    public const string Night = "#10142A";      // dark ground (indigo-black)
    public const string NightRaised = "#181D38";
    public const string NightThread = "#2A3052";
    public const string Bleach = "#F3EEE3";     // dark text

    /// <summary>Dye colours per kind of action: (light theme, dark theme).</summary>
    public static readonly IReadOnlyDictionary<string, (string Light, string Dark)> Hues = new Dictionary<string, (string, string)>
    {
        ["Indigo"] = ("#2F3D9E", "#9AA6FF"),   // the app itself, settings, chat
        ["Madder"] = ("#B5382A", "#FF8A73"),   // rewriting and styles
        ["Plum"] = ("#7B3A8C", "#D59BE8"),     // summary, explain, models
        ["Peacock"] = ("#0F7480", "#5FD0D9"),  // translate, listen
        ["Leaf"] = ("#2E7D4F", "#6FD39A"),     // proofreading, success
        ["Turmeric"] = ("#9A6400", "#F2B544"), // warnings, keys
        ["Muted"] = ("#6B6577", "#A8A3B8"),    // neutral
    };

    public static readonly FontFamily Display = new("avares://Rewordly/Assets/Fonts#DM Serif Display");

    public static Bitmap Asset(string name) => new(AssetLoader.Open(new Uri($"avares://Rewordly/Assets/Brand/{name}")));

    private static Bitmap? _headerTile, _borderStrip;

    /// <summary>The block-printed cloth, tiled at its natural scale.</summary>
    public static ImageBrush ClothBrush() => new(_headerTile ??= Asset("header-tile.jpg"))
    {
        TileMode = TileMode.Tile, Stretch = Stretch.Fill,
        DestinationRect = new RelativeRect(0, 0, 86, 106, RelativeUnit.Absolute),
    };

    /// <summary>The cloth's printed border (turmeric vine, madder flowers), used as the edge of every header.</summary>
    public static Control BorderStrip(double height) => new Border
    {
        Height = height, ClipToBounds = true,
        Child = new Image
        {
            Source = _borderStrip ??= Asset("border-strip.jpg"), Stretch = Stretch.Fill,
            Height = height, Width = height * _borderStrip!.PixelSize.Width / _borderStrip.PixelSize.Height,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        },
    };

    /// <summary>Indigo dye laid over the cloth: dense where the title sits, thinning to show the print on the right.</summary>
    public static LinearGradientBrush DyeOverlay() => new()
    {
        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#F218206A"), 0),
            new GradientStop(Color.Parse("#EA1C2270"), 0.55),
            new GradientStop(Color.Parse("#992A1F6E"), 0.8),
            new GradientStop(Color.Parse("#554A2470"), 1),
        },
    };

    /// <summary>Indigo → plum dye for small solid marks (step badges, chips that carry white text).</summary>
    public static LinearGradientBrush IndigoDye() => Gradient("#2F3D9E", "#1F2878", "#4A2470");

    private static LinearGradientBrush MadderBrush() => Gradient("#C8412F", "#A3303F");
    private static LinearGradientBrush MadderHoverBrush() => Gradient("#B83524", "#962A3A");
    private static LinearGradientBrush MadderPressedBrush() => Gradient("#A32E20", "#82232F");

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
            Accent = Color.Parse("#2F3D9E"), RegionColor = Color.Parse(Cotton), BaseHigh = Color.Parse(Ink),
        };
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = Color.Parse("#7F8CF0"), RegionColor = Color.Parse(Night), BaseHigh = Color.Parse(Bleach),
        };
        app.Styles.Add(fluent);

        var light = new ResourceDictionary();
        var dark = new ResourceDictionary();
        foreach (var (name, (l, d)) in Hues)
        {
            var lc = Color.Parse(l); var dc = Color.Parse(d);
            light["Hue." + name] = new SolidColorBrush(lc);
            dark["Hue." + name] = new SolidColorBrush(dc);
            light["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x1C, lc.R, lc.G, lc.B));
            dark["Chip." + name] = new SolidColorBrush(Color.FromArgb(0x2A, dc.R, dc.G, dc.B));
        }
        light["Rw.CardBackground"] = B(Paper);
        dark["Rw.CardBackground"] = B(NightRaised);
        light["Rw.CardBorder"] = B(Thread);
        dark["Rw.CardBorder"] = B(NightThread);
        light["Rw.Ink"] = B(Ink);
        dark["Rw.Ink"] = B(Bleach);

        // Secondary controls sit on the ramp: paper with a thread-coloured hairline, or raised night.
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
        Surface(light, Paper, "#F5F0E6", "#ECE5D7", "#DCD4C4", Paper, Paper);
        Surface(dark, "#1E2444", "#262D52", "#181D38", "#363E66", "#0B0E1F", "#1A1F3C");
        app.Resources["ButtonBorderThemeThickness"] = new Thickness(1);
        app.Resources.ThemeDictionaries[ThemeVariant.Light] = light;
        app.Resources.ThemeDictionaries[ThemeVariant.Dark] = dark;
        app.Resources["ControlCornerRadius"] = new CornerRadius(7);
        app.Resources["OverlayCornerRadius"] = new CornerRadius(12);

        // The one main action per window: madder dye with white text. Disabled keeps Fluent's grey.
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
        app.Styles.Add(Accent(x => x!, MadderBrush()));
        app.Styles.Add(Accent(x => x!.Class(":pointerover"), MadderHoverBrush()));
        app.Styles.Add(Accent(x => x!.Class(":pressed"), MadderPressedBrush()));

        // Progress runs through the dyes in printing order.
        app.Styles.Add(new Style(x => x.OfType<ProgressBar>())
        {
            Setters = { new Setter(ProgressBar.ForegroundProperty, Gradient("#2F3D9E", "#7B3A8C", "#C8412F", "#E8A33D")) },
        });
    }
}
