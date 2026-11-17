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

        var lang = s.PromptLanguage;
        bool isHi = lang.Equals("hi", StringComparison.OrdinalIgnoreCase);

        int words = TextUtil.CountWords(text);
        var wordCountDisplay = isHi ? $"{words} शब्द ({words} word{(words == 1 ? "" : "s")})" : $"{words} word{(words == 1 ? "" : "s")}";
        items.Add(new MenuItem { Header = $"Rewordly  ·  {AppController.Display(s.Active.Model)}  ·  {wordCountDisplay}", IsEnabled = false, Icon = Ui.Logo(16) });
        items.Add(new Separator());

        // Proofreading first: the most common, no further choice needed.
        items.Add(With(Act(isHi ? "सुधारें और बेहतर बनाएं (Improve)" : "Correct and improve", new ActionSpec(ActionKind.Improve)), "TextGrammarWand", "Green"));
        items.Add(With(Act(isHi ? "सिर्फ गलतियाँ ठीक करें (Fix errors)" : "Fix errors only", new ActionSpec(ActionKind.Fix)), "TextGrammarCheckmark", "Green"));
        items.Add(new Separator());

        // Recently used styles stay one click away; every style lives in one submenu.
        foreach (var r in s.RecentStyles) items.Add(With(Act(Catalog.StyleLabel(r, lang), new ActionSpec(ActionKind.Style, r)), "PenSparkle", "Pink"));

        var styles = With(new MenuItem { Header = isHi ? "किसी स्टाइल में दोबारा लिखें (Rewrite in a style)" : "Rewrite in a style" }, "TextEffects", "Pink");
        foreach (var st in Catalog.MainStyles)
            styles.Items.Add(Act(Catalog.StyleLabel(st, lang), new ActionSpec(ActionKind.Style, st)));
        styles.Items.Add(new Separator());

        var other = With(new MenuItem { Header = isHi ? "अन्य स्टाइल (Other styles)" : "Other styles" }, "TextEditStyle", "Pink");
        foreach (var st in Catalog.OtherStyles) other.Items.Add(Act(Catalog.StyleLabel(st, lang), new ActionSpec(ActionKind.Style, st)));
        styles.Items.Add(other);

        // My styles
        var mine = With(new MenuItem { Header = isHi ? "मेरे बनाए स्टाइल (My styles)" : "My styles" }, "Edit", "Pink");
        var add = With(new MenuItem { Header = isHi ? "नया स्टाइल जोड़ें… (Add a style…)" : "Add a style…" }, "Add", "Pink");
        add.Click += async (_, _) =>
        {
            menu.Close();
            var title = isHi ? "नया स्टाइल जोड़ें" : "Add a style";
            var promptMsg = isHi
                ? "स्टाइल कैसा होना चाहिए? (जैसे \"लिंक्डइन पोस्ट\" या \"ग्राहक को विनम्र जवाब\"):"
                : "Describe the style (for example \"LinkedIn post\" or \"polite customer reply\"):";
            var name = await Dialog.InputAsync(title, promptMsg, AppSettings.MaxCustomStyleLength);
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
                ToolTip.SetTip(item, isHi ? "हटाने के लिए राइट-क्लिक करें" : "Right-click to delete");
                item.AddHandler(InputElement.PointerPressedEvent, async (object? sender, PointerPressedEventArgs e) =>
                {
                    if (!e.GetCurrentPoint(item).Properties.IsRightButtonPressed) return;
                    e.Handled = true;
                    menu.Close();
                    var confirmMsg = isHi ? $"क्या आप स्टाइल \"{style}\" को हटाना चाहते हैं?" : $"Delete the style \"{style}\"? This removes it from My styles.";
                    if (await Dialog.AskAsync("Rewordly", confirmMsg, isHi ? "हटाएं (Delete)" : "Delete style", isHi ? "रहने दें (Keep)" : "Keep it"))
                    {
                        s.RemoveCustomStyle(style);
                        app.SaveSettings();
                    }
                }, RoutingStrategies.Tunnel);
                mine.Items.Add(item);
            }
        }
        styles.Items.Add(mine);
        items.Add(styles);

        var agent = With(new MenuItem { Header = isHi ? "AI एजेंट प्रॉम्प्ट में बदलें (AI agent prompt)" : "Rewrite as AI agent prompt" }, "Bot", "Pink");
        foreach (var group in Prompts.AgentTemplates.GroupBy(t => t.Category))
        {
            if (group.Key.Length > 0)
            {
                if (agent.Items.Count > 0) agent.Items.Add(new Separator());
                agent.Items.Add(new MenuItem { Header = group.Key, IsEnabled = false });
            }
            foreach (var t in group) agent.Items.Add(Act(t.Label, new ActionSpec(ActionKind.AgentPrompt, t.Id)));
        }
        items.Add(agent);
        items.Add(new Separator());

        var sum = With(new MenuItem { Header = isHi ? "समरी (Summary)" : "Summary" }, "TextBulletListSquare", "Violet");
        foreach (var p in Catalog.SummaryPercents)
        {
            var summaryLabel = isHi
                ? $"लगभग {p}% लंबाई तक (~{TextUtil.SummaryWordLimit(text, p)} शब्द)"
                : $"Up to {p}% of the length (~{TextUtil.SummaryWordLimit(text, p)} words)";
            sum.Items.Add(Act(summaryLabel, new ActionSpec(ActionKind.Summary, p.ToString())));
        }
        items.Add(sum);

        var exp = With(new MenuItem { Header = isHi ? "समझाएं (Explain)" : "Explain" }, "Lightbulb", "Violet");
        exp.Items.Add(new MenuItem { Header = isHi ? "पढ़ने वाले की समझ का स्तर:" : "Reader's knowledge level:", IsEnabled = false });
        foreach (var l in Catalog.Levels) exp.Items.Add(Act(Catalog.LevelLabel(l.Id, lang), new ActionSpec(ActionKind.Explain, l.Id)));
        items.Add(exp);

        var tr = With(new MenuItem { Header = isHi ? "अनुवाद करें (Translate into)" : "Translate into" }, "Translate", "Orange");
        foreach (var c in TranslateItems(app, langChoice => app.Run(new ActionSpec(ActionKind.Translate, langChoice), text, target), menu.Close)) tr.Items.Add(c);
        items.Add(tr);
        items.Add(new Separator());

        items.Add(With(Act(isHi ? "बातचीत (Chat…)" : "Chat…", new ActionSpec(ActionKind.Chat)), "ChatSparkle", "Blue"));
        items.Add(With(Act(isHi ? "बोलकर सुनाएं (Listen)" : "Listen", new ActionSpec(ActionKind.Listen)), "Speaker2", "Orange"));
        items.Add(new Separator());
        items.Add(With(ModelMenu(app, menu.Close), "BrainCircuit", "Blue"));
        var cancel = With(new MenuItem { Header = isHi ? "रद्द करें (Cancel)" : "Cancel" }, "Dismiss", "Muted");
        cancel.Click += (_, _) => menu.Close();
        items.Add(cancel);

        menu.ItemsSource = items;
        return menu;
    }

    private static MenuItem With(MenuItem m, string icon, string hue)
    {
        m.Icon = Ui.Glyph(icon, 16, hue);
        return m;
    }

    /// <summary>Recent languages, main list, other languages and a free-text option.</summary>
    public static List<Control> TranslateItems(AppController app, Action<string> onPick, Action? close = null)
    {
        var s = app.Settings;
        var isHi = s.PromptLanguage.Equals("hi", StringComparison.OrdinalIgnoreCase);
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
        var more = new MenuItem { Header = isHi ? "अन्य भाषाएँ (Other languages)" : "Other languages" };
        foreach (var l in Catalog.OtherLanguages.Concat(s.ExtraLanguages).Distinct(StringComparer.OrdinalIgnoreCase)) more.Items.Add(Lang(l));
        list.Add(more);
        var custom = new MenuItem { Header = isHi ? "कोई दूसरी भाषा… (Another language…)" : "Another language…" };
        custom.Click += async (_, _) =>
        {
            close?.Invoke();
            var title = isHi ? "अनुवाद" : "Translate";
            var promptMsg = isHi
                ? "किस भाषा में अनुवाद करना है? (जैसे \"Hinglish\", \"Bhojpuri\", \"Brazilian Portuguese\"):"
                : "Target language (for example \"Hinglish\", \"Bhojpuri\", \"Brazilian Portuguese\"):";
            var lang = await Dialog.InputAsync(title, promptMsg, 40);
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
            var i = new MenuItem { Header = model, ToggleType = MenuItemToggleType.Radio, IsChecked = active, GroupName = "rewordly-model" };
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
