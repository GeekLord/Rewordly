using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Rewordly.Core;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Builds the action menu shown at the pointer after the shortcut.</summary>
internal static class MenuFactory
{
    public static ContextMenu Build(AppController app, string text, WindowFocus.Target target)
    {
        var s = app.Settings;
        var menu = new ContextMenu();
        var items = new List<Control>();

        MenuItem Act(string label, ActionSpec a)
        {
            var i = new MenuItem { Header = label };
            i.Click += (_, _) => { menu.Close(); app.Run(a, text, target); };
            return i;
        }

        int words = TextUtil.CountWords(text);
        items.Add(new MenuItem { Header = $"Rewordly  ·  {AppController.Display(s.Active.Model)}  ·  {words} word{(words == 1 ? "" : "s")}", IsEnabled = false });
        items.Add(new Separator());

        if (s.RecentStyles.Count > 0)
        {
            foreach (var r in s.RecentStyles) items.Add(Act(r, new ActionSpec(ActionKind.Style, r)));
            items.Add(new Separator());
        }

        foreach (var st in Catalog.MainStyles.Where(x => !s.RecentStyles.Contains(x, StringComparer.OrdinalIgnoreCase)))
            items.Add(Act(st, new ActionSpec(ActionKind.Style, st)));

        var other = new MenuItem { Header = "Other styles" };
        foreach (var st in Catalog.OtherStyles) other.Items.Add(Act(st, new ActionSpec(ActionKind.Style, st)));
        items.Add(other);

        // My styles
        var mine = new MenuItem { Header = "My styles" };
        var add = new MenuItem { Header = "Add a style…" };
        add.Click += async (_, _) =>
        {
            menu.Close();
            var name = await Dialog.InputAsync("Add a style", "Describe the style (for example \"LinkedIn post\" or \"polite customer reply\"):", AppSettings.MaxCustomStyleLength);
            if (name == null) return;
            var err = s.AddCustomStyle(name);
            if (err != null) { await Dialog.InfoAsync("Rewordly", err); return; }
            app.SaveSettings();
            app.Run(new ActionSpec(ActionKind.Style, name.Trim()), text, target);
        };
        mine.Items.Add(add);
        if (s.CustomStyles.Count > 0)
        {
            mine.Items.Add(new Separator());
            foreach (var cs in s.CustomStyles)
            {
                var style = cs;
                var item = Act(style, new ActionSpec(ActionKind.Style, style));
                ToolTip.SetTip(item, "Right-click to delete");
                item.AddHandler(InputElement.PointerPressedEvent, async (object? sender, PointerPressedEventArgs e) =>
                {
                    if (!e.GetCurrentPoint(item).Properties.IsRightButtonPressed) return;
                    e.Handled = true;
                    menu.Close();
                    if (await Dialog.AskAsync("Rewordly", $"Delete the style \"{style}\"?"))
                    {
                        s.RemoveCustomStyle(style);
                        app.SaveSettings();
                    }
                }, RoutingStrategies.Tunnel);
                mine.Items.Add(item);
            }
        }
        items.Add(mine);
        items.Add(new Separator());

        items.Add(Act("Correct and improve", new ActionSpec(ActionKind.Improve)));
        items.Add(Act("Fix errors only", new ActionSpec(ActionKind.Fix)));

        var sum = new MenuItem { Header = "Summary" };
        foreach (var p in Catalog.SummaryPercents)
            sum.Items.Add(Act($"Up to {p}% of the length (~{TextUtil.SummaryWordLimit(text, p)} words)", new ActionSpec(ActionKind.Summary, p.ToString())));
        items.Add(sum);

        var exp = new MenuItem { Header = "Explain" };
        exp.Items.Add(new MenuItem { Header = "Reader's knowledge level:", IsEnabled = false });
        foreach (var l in Catalog.Levels) exp.Items.Add(Act(l.Label, new ActionSpec(ActionKind.Explain, l.Id)));
        items.Add(exp);

        var tr = new MenuItem { Header = "Translate into" };
        foreach (var c in TranslateItems(app, lang => app.Run(new ActionSpec(ActionKind.Translate, lang), text, target), menu.Close)) tr.Items.Add(c);
        items.Add(tr);
        items.Add(new Separator());

        items.Add(Act("Chat…", new ActionSpec(ActionKind.Chat)));
        items.Add(Act("Listen", new ActionSpec(ActionKind.Listen)));
        items.Add(new Separator());
        items.Add(ModelMenu(app, menu.Close));
        var cancel = new MenuItem { Header = "Cancel" };
        cancel.Click += (_, _) => menu.Close();
        items.Add(cancel);

        menu.ItemsSource = items;
        return menu;
    }

    /// <summary>Recent languages, main list, other languages and a free-text option.</summary>
    public static List<Control> TranslateItems(AppController app, Action<string> onPick, Action? close = null)
    {
        var s = app.Settings;
        var list = new List<Control>();
        MenuItem Lang(string lang)
        {
            var i = new MenuItem { Header = lang };
            i.Click += (_, _) => { close?.Invoke(); onPick(lang); };
            return i;
        }
        if (s.RecentLanguages.Count > 0)
        {
            foreach (var l in s.RecentLanguages) list.Add(Lang(l));
            list.Add(new Separator());
        }
        foreach (var l in Catalog.MainLanguages) list.Add(Lang(l));
        var more = new MenuItem { Header = "Other languages" };
        foreach (var l in Catalog.OtherLanguages.Concat(s.ExtraLanguages).Distinct(StringComparer.OrdinalIgnoreCase)) more.Items.Add(Lang(l));
        list.Add(more);
        var custom = new MenuItem { Header = "Another language…" };
        custom.Click += async (_, _) =>
        {
            close?.Invoke();
            var lang = await Dialog.InputAsync("Translate", "Target language (for example \"Hinglish\", \"Bhojpuri\", \"Brazilian Portuguese\"):", 40);
            if (string.IsNullOrWhiteSpace(lang)) return;
            if (!Catalog.AllLanguages.Contains(lang, StringComparer.OrdinalIgnoreCase) && !s.ExtraLanguages.Contains(lang, StringComparer.OrdinalIgnoreCase))
            {
                s.ExtraLanguages.Add(lang);
                app.SaveSettings();
            }
            onPick(lang);
        };
        list.Add(custom);
        return list;
    }

    private static IEnumerable<(ProviderPreset Preset, string Model, bool Active)> ModelChoices(AppController app)
    {
        var s = app.Settings;
        foreach (var preset in ProviderPresets.All)
        {
            if (!s.Providers.TryGetValue(preset.Id, out var cfg)) continue;
            if (preset.NeedsKey && string.IsNullOrEmpty(app.Store.GetApiKey(s, preset.Id))) continue;
            var models = cfg.RecentModels.ToList();
            if (!string.IsNullOrWhiteSpace(cfg.Model) && !models.Contains(cfg.Model)) models.Insert(0, cfg.Model);
            foreach (var m in models)
                yield return (preset, m, preset.Id.Equals(s.ActiveProvider, StringComparison.OrdinalIgnoreCase) && m == s.Active.Model);
        }
    }

    /// <summary>Quick model switcher for the action menu.</summary>
    public static MenuItem ModelMenu(AppController app, Action? close = null)
    {
        var root = new MenuItem { Header = $"Model: {AppController.Display(app.Settings.Active.Model)}" };
        string? lastProvider = null;
        foreach (var (preset, model, active) in ModelChoices(app))
        {
            if (preset.Id != lastProvider) { root.Items.Add(new MenuItem { Header = preset.Name, IsEnabled = false }); lastProvider = preset.Id; }
            var i = new MenuItem { Header = (active ? "✓ " : "   ") + model };
            var id = preset.Id; var m = model;
            i.Click += (_, _) => { close?.Invoke(); app.SelectModel(id, m); };
            root.Items.Add(i);
        }
        if (root.Items.Count > 0) root.Items.Add(new Separator());
        var more = new MenuItem { Header = "Choose another model…" };
        more.Click += (_, _) => { close?.Invoke(); app.ShowSettings(); };
        root.Items.Add(more);
        return root;
    }

    /// <summary>Same switcher for the tray icon's native menu.</summary>
    public static NativeMenuItem ModelNativeMenu(AppController app)
    {
        var root = new NativeMenuItem($"Model: {AppController.Display(app.Settings.Active.Model)}") { Menu = new NativeMenu() };
        string? lastProvider = null;
        foreach (var (preset, model, active) in ModelChoices(app))
        {
            if (preset.Id != lastProvider) { root.Menu.Add(new NativeMenuItem(preset.Name) { IsEnabled = false }); lastProvider = preset.Id; }
            var i = new NativeMenuItem("   " + model) { ToggleType = NativeMenuItemToggleType.Radio, IsChecked = active };
            var id = preset.Id; var m = model;
            i.Click += (_, _) => app.SelectModel(id, m);
            root.Menu.Add(i);
        }
        if (root.Menu.Items.Count > 0) root.Menu.Add(new NativeMenuItemSeparator());
        var more = new NativeMenuItem("Choose another model…");
        more.Click += (_, _) => app.ShowSettings();
        root.Menu.Add(more);
        return root;
    }
}
