using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Rewordly.Core;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>AI provider/model, shortcuts and behaviour, prompt editor, about.</summary>
internal sealed class SettingsWindow : Window
{
    // Release builds stamp this via -p:Version; drop the "+commit" suffix the SDK appends.
    private static readonly string AppVersion =
        typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    private readonly AppController _app;
    private readonly AppSettings _work;
    private readonly Dictionary<string, string?> _pendingKeys = new(StringComparer.OrdinalIgnoreCase);
    private string _provider;
    private bool _loading;

    internal readonly ComboBox ProviderBox, ModelList, ThemeBox, PromptKeyBox, PromptLanguageBox;
    internal readonly TextBox BaseUrl, ApiKey, Model, PromptEditor;
    internal readonly TextBlock ProviderNotes, KeyInfo, TestStatus;
    internal readonly CheckBox ShowKey, DefaultTemp, StartAtLogin, DoubleCopy, AltShortcut, RestoreClip;
    internal readonly NumericUpDown Temp, Timeout, WindowMs, MaxWords;
    internal readonly Expander Advanced;
    private string _promptKey = Prompts.KeyStyle;

    public SettingsWindow(AppController app)
    {
        _app = app;
        _work = Clone(app.Settings);
        _provider = ProviderPresets.Get(_work.ActiveProvider).Id;
        Ui.Prepare(this, "Rewordly – Settings", 760, 720);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // ---------------------------------------------------------------- AI model tab
        ProviderBox = new ComboBox { ItemsSource = ProviderPresets.All.Select(p => p.Name).ToList(), MinWidth = 320 };
        ProviderNotes = Ui.Hint("");
        BaseUrl = new TextBox { MinWidth = 420 };
        ApiKey = new TextBox { MinWidth = 420, PasswordChar = '•' };
        ShowKey = new CheckBox { Content = "Show", Margin = new Thickness(8, 0, 0, 0) };
        ShowKey.IsCheckedChanged += (_, _) => ApiKey.PasswordChar = ShowKey.IsChecked == true ? '\0' : '•';
        var getKey = Ui.Btn("Get a key", icon: "Key", hue: "Pink", onClick: () => { var u = ProviderPresets.Get(_provider).KeyHelpUrl; if (u.Length > 0) Os.Open(u); });
        ToolTip.SetTip(getKey, "Opens the provider's page where you can create an API key.");
        KeyInfo = Ui.Hint("");
        Model = new TextBox { MinWidth = 420 };
        ModelList = new ComboBox { MinWidth = 280, PlaceholderText = "Pick from available models", Margin = new Thickness(0, 0, 8, 6) };
        ModelList.SelectionChanged += (_, _) => { if (ModelList.SelectedItem is string m) Model.Text = m; };
        var refresh = Ui.Btn("Refresh list", RefreshModelsAsync, icon: "ArrowSync", hue: "Violet");
        ToolTip.SetTip(refresh, "Load the models this provider offers for your key.");
        var test = Ui.Btn("Test connection", TestAsync, icon: "PlugConnected", hue: "Green");
        ToolTip.SetTip(test, "Send a tiny request with the provider, key and model above (nothing is saved).");
        TestStatus = Ui.Hint("");
        DefaultTemp = new CheckBox { Content = "Use the model's default temperature", Margin = new Thickness(0, 0, 12, 0) };
        Temp = new NumericUpDown { Minimum = 0, Maximum = 2, Increment = 0.1m, FormatString = "0.0", Width = 130 };
        DefaultTemp.IsCheckedChanged += (_, _) => Temp.IsEnabled = DefaultTemp.IsChecked != true;
        Timeout = new NumericUpDown { Minimum = 10, Maximum = 600, Increment = 10, FormatString = "0", Width = 130 };

        ProviderBox.SelectionChanged += (_, _) =>
        {
            if (_loading || ProviderBox.SelectedIndex < 0) return;
            StoreProviderFields();
            _provider = ProviderPresets.All[ProviderBox.SelectedIndex].Id;
            LoadProviderFields();
        };

        ShowKey.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        ShowKey.Margin = new Thickness(8, 0, 8, 6);
        ApiKey.Margin = new Thickness(0, 0, 0, 6);
        Advanced = new Expander
        {
            Header = Ui.TabHeader("Advanced: API address, temperature, timeout", "Settings", "Muted"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 16, 0, 0),
            Content = new StackPanel
            {
                Children =
                {
                    Ui.Label("API address"), BaseUrl,
                    Ui.Hint("Change this only for your own server or a proxy. Leave the default otherwise."),
                    Ui.Label("Temperature"), Ui.Row(DefaultTemp, Temp),
                    Ui.Hint("Lower values give more predictable wording; higher values give more varied wording."),
                    Ui.Label("Timeout"), Ui.Row(Timeout, Unit("seconds")),
                },
            },
        };
        var providerHead = Ui.Section("Provider", "PlugConnected", "Blue");
        providerHead.Margin = new Thickness(0, 0, 0, 2);
        var ai = new StackPanel
        {
            Children =
            {
                providerHead, ProviderBox, ProviderNotes,
                Ui.Section("API key", "Key", "Pink"), Ui.Row(ApiKey, ShowKey, getKey), KeyInfo,
                Ui.Section("Model", "BrainCircuit", "Violet"), Model, Ui.Row(ModelList, refresh),
                Ui.Hint("Type a model name, or click Refresh list and pick one."),
                Ui.Row(test), TestStatus,
                Advanced,
            },
        };
        ProviderNotes.Margin = new Thickness(0, 4, 0, 0);

        // ---------------------------------------------------------------- behaviour tab
        ThemeBox = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, Width = 160 };
        StartAtLogin = new CheckBox { Content = $"Start Rewordly when I log in to {Os.Name}" };
        DoubleCopy = new CheckBox { Content = $"Open the menu when I press {Os.ModLabel}+C twice (hold {Os.ModLabel})" };
        WindowMs = new NumericUpDown { Minimum = 250, Maximum = 2000, Increment = 50, FormatString = "0", Width = 140 };
        AltShortcut = new CheckBox { Content = $"Also open the menu with {Os.AltShortcutLabel} (doesn't touch your clipboard)" };
        RestoreClip = new CheckBox { Content = "Put my previous clipboard back after copying and pasting" };
        MaxWords = new NumericUpDown { Minimum = 50, Maximum = 20000, Increment = 100, FormatString = "0", Width = 140 };
        var interval = Ui.Row(Ui.Text("Time allowed between the two presses:"), WindowMs, Unit("ms"));
        interval.Margin = new Thickness(28, 0, 0, 4);
        DoubleCopy.IsCheckedChanged += (_, _) => interval.IsEnabled = DoubleCopy.IsChecked == true;
        var shortcutsHead = Ui.Section("Opening the menu", "Keyboard", "Blue");
        shortcutsHead.Margin = new Thickness(0, 0, 0, 6);
        var platformNote = Os.IsWayland
            ? "Wayland session detected: system-wide shortcuts only work in X11/XWayland apps."
            : Os.IsMac ? "macOS: Rewordly must be allowed under Privacy & Security > Accessibility." : "";
        var behaviour = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                shortcutsHead,
                DoubleCopy, interval,
                AltShortcut,
                Ui.Section("Clipboard", "Clipboard", "Orange"), RestoreClip,
                Ui.Section("Selection limit", "TextWordCount", "Violet"),
                Ui.Row(Ui.Text("Maximum words per selection:"), MaxWords),
                Ui.Hint("Longer selections are not sent to the AI; Rewordly shows a message instead."),
                Ui.Section("Appearance and startup", "PaintBrush", "Pink"),
                Ui.Row(Ui.Text("Theme:"), ThemeBox),
                StartAtLogin,
            },
        };
        if (platformNote.Length > 0) behaviour.Children.Insert(1, Ui.Hint(platformNote));

        // ---------------------------------------------------------------- prompts tab
        var keys = Prompts.Labels.Keys.ToList();
        PromptKeyBox = new ComboBox { ItemsSource = keys.Select(k => Prompts.Labels[k]).ToList(), MinWidth = 320, Margin = new Thickness(0, 0, 8, 6) };
        var langLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English (en)",
            ["it"] = "Italiano (it)",
            ["es"] = "Español (es)",
            ["fr"] = "Français (fr)",
            ["de"] = "Deutsch (de)",
            ["hi"] = "हिन्दी / Hindi (hi)",
        };
        var langs = Prompts.SupportedLanguages.ToList();
        PromptLanguageBox = new ComboBox { ItemsSource = langs.Select(l => langLabels.TryGetValue(l, out var name) ? name : l).ToList(), MinWidth = 200 };
        PromptEditor = new TextBox { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 340, FontFamily = "monospace", FontSize = 13 };

        PromptKeyBox.SelectionChanged += (_, _) =>
        {
            if (_loading || PromptKeyBox.SelectedIndex < 0) return;
            StorePrompt();
            _promptKey = keys[PromptKeyBox.SelectedIndex];
            PromptEditor.Text = Prompts.Template(_work, _promptKey);
        };
        PromptLanguageBox.SelectionChanged += (_, _) =>
        {
            if (_loading || PromptLanguageBox.SelectedIndex < 0) return;
            StorePrompt();
            _work.PromptLanguage = langs[PromptLanguageBox.SelectedIndex];
            PromptEditor.Text = Prompts.Template(_work, _promptKey);
        };
        var reset = Ui.Btn("Reset to default", icon: "ArrowUndo", hue: "Muted", onClick: () =>
        {
            _work.PromptOverrides.Remove(_promptKey);
            PromptEditor.Text = Prompts.GetDefault(_promptKey, _work.PromptLanguage);
        });
        ToolTip.SetTip(reset, "Replace this prompt with Rewordly's built-in version.");
        var promptsIntro = Ui.Hint("These are the instructions Rewordly sends to the AI for each action. Most people never need to change them.");
        promptsIntro.Margin = new Thickness(0, 0, 0, 6);
        var prompts = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                promptsIntro,
                Ui.Row(Ui.Text("Template language:"), PromptLanguageBox),
                Ui.Label("Action"), Ui.Row(PromptKeyBox, reset),
                Ui.Label("Prompt"), PromptEditor,
                Ui.Hint("Placeholders: {selectedText} or {text}, {selectedStyle}, {num}, {value}, {currentResultText}."),
            },
        };

        // ---------------------------------------------------------------- about tab
        var brand = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*") };
        var logo = Ui.Logo(64);
        logo.Margin = new Thickness(0, 0, 16, 0);
        var brandText = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Ui.Text("Rewordly " + AppVersion, bold: true, size: 20),
                Ui.Text("Rewrite, fix, summarise, explain, translate, chat about and listen to any selected text, in any app, on Windows, macOS and Linux.", muted: true),
            },
        };
        Grid.SetColumn(brandText, 1);
        brand.Children.Add(logo);
        brand.Children.Add(brandText);
        Control Fact(string icon, string hue, string text)
        {
            var t = Ui.Text(text);
            var r = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 2), Children = { Ui.Chip(icon, hue, 28), t } };
            return r;
        }
        var about = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Card(brand, 18),
                Ui.Section("This computer", "Window", "Violet"),
                Fact("Window", "Violet", $"System: {Os.Name}{(Os.IsWayland ? " (Wayland)" : "")}"),
                Fact("Speaker2", "Orange", $"Speech: {_app.Tts.EngineName}"),
                Fact("LockClosed", "Pink", $"Key storage: {_app.Secrets.Backend}"),
                Fact("FolderOpen", "Blue", $"Settings folder: {_app.Store.Folder}"),
                Ui.Row(Ui.Btn("Open settings folder", () => { Directory.CreateDirectory(_app.Store.Folder); Os.Open(_app.Store.Folder); }, icon: "FolderOpen"), Ui.Btn("Quick guide", _app.ShowGuide, icon: "BookOpen", hue: "Orange")),
                Ui.Section("Privacy", "ShieldLock", "Green"),
                Ui.Text("Your text is sent only to the AI provider you choose. Nothing is sent anywhere else.", muted: true),
            },
        };

        Control Pad(Control c) => new ScrollViewer { Content = new Border { Padding = new Thickness(14), Child = c } };
        var tabs = new TabControl
        {
            Padding = new Thickness(8, 0),
            ItemsSource = new[]
            {
                new TabItem { FontSize = 15, Header = Ui.TabHeader("AI model", "BrainCircuit", "Blue"), Content = Pad(ai) },
                new TabItem { FontSize = 15, Header = Ui.TabHeader("Shortcuts & behaviour", "Keyboard", "Pink"), Content = Pad(behaviour) },
                new TabItem { FontSize = 15, Header = Ui.TabHeader("Prompts", "TextDescription", "Violet"), Content = Pad(prompts) },
                new TabItem { FontSize = 15, Header = Ui.TabHeader("About", "Info", "Orange"), Content = Pad(about) },
            },
        };
        var save = Ui.Btn("Save", SaveAndClose, primary: true, icon: "CheckmarkCircle");
        var cancel = Ui.Btn("Cancel", Close);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 4, 4, 8), Children = { save, cancel } };
        Content = Ui.Framed(Ui.Header("Settings", "Choose your AI provider and model, shortcuts and prompts", "Settings"), Ui.Rows("*,Auto", tabs, buttons));

        LoadAll();
        interval.IsEnabled = DoubleCopy.IsChecked == true;
    }

    private static TextBlock Unit(string text)
    {
        var t = Ui.Text(text, muted: true);
        t.Margin = new Thickness(8, 0, 0, 0);
        return t;
    }

    private static AppSettings Clone(AppSettings s)
    {
        var c = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(s))!;
        c.Providers = new Dictionary<string, ProviderConfig>(c.Providers, StringComparer.OrdinalIgnoreCase);
        c.PromptOverrides = new Dictionary<string, string>(c.PromptOverrides, StringComparer.OrdinalIgnoreCase);
        return c;
    }

    private void LoadAll()
    {
        _loading = true;
        ProviderBox.SelectedIndex = Array.FindIndex(ProviderPresets.All, p => p.Id == _provider);
        LoadProviderFields();
        DefaultTemp.IsChecked = _work.Temperature == null;
        Temp.Value = (decimal)(_work.Temperature ?? 0.7);
        Temp.IsEnabled = _work.Temperature != null;
        Timeout.Value = _work.TimeoutSeconds;
        ThemeBox.SelectedIndex = _work.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        StartAtLogin.IsChecked = _work.StartWithWindows;
        DoubleCopy.IsChecked = _work.TriggerDoubleCtrlC;
        WindowMs.Value = _work.DoubleCopyWindowMs;
        AltShortcut.IsChecked = _work.TriggerAltShortcut;
        RestoreClip.IsChecked = _work.RestoreClipboard;
        MaxWords.Value = _work.MaxWords;
        var keys = Prompts.Labels.Keys.ToList();
        PromptKeyBox.SelectedIndex = Math.Max(0, keys.FindIndex(k => k.Equals(_promptKey, StringComparison.OrdinalIgnoreCase)));
        var langs = Prompts.SupportedLanguages.ToList();
        PromptLanguageBox.SelectedIndex = Math.Max(0, langs.FindIndex(l => l.Equals(_work.PromptLanguage, StringComparison.OrdinalIgnoreCase)));
        PromptEditor.Text = Prompts.Template(_work, _promptKey);
        _loading = false;
    }

    private void LoadProviderFields()
    {
        var was = _loading;
        _loading = true;
        var p = ProviderPresets.Get(_provider);
        var cfg = _work.GetProvider(_provider);
        ProviderNotes.Text = p.Notes;
        BaseUrl.Text = string.IsNullOrWhiteSpace(cfg.BaseUrl) ? p.DefaultBaseUrl : cfg.BaseUrl;
        BaseUrl.Watermark = p.DefaultBaseUrl.Length > 0 ? p.DefaultBaseUrl : "https://your-server/v1";
        ApiKey.Text = _pendingKeys.TryGetValue(_provider, out var pending) ? pending : _app.Store.HasStoredKey(_work, _provider) ? _app.Store.GetApiKey(_work, _provider) : "";
        var env = SettingsStore.EnvironmentKey(_provider) != null ? p.KeyEnvironmentVariables.First(v => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v))) : null;
        KeyInfo.Text = (p.NeedsKey ? "Required. " : "Not required. ") + $"Saved securely with: {_app.Secrets.Backend}." +
                       (env != null ? $" If left empty, the {env} environment variable is used." :
                        p.KeyEnvironmentVariables.Length > 0 ? $" You can also set {string.Join(" or ", p.KeyEnvironmentVariables)} instead." : "");
        Model.Text = cfg.Model.Length > 0 ? cfg.Model : p.DefaultModel;
        ModelList.ItemsSource = cfg.RecentModels.ToList();
        Model.Watermark = p.DefaultModel.Length > 0 ? p.DefaultModel : "Model name";
        TestStatus.Text = "";
        // Open Advanced when the address matters: a custom server, or an address changed from the default.
        var customUrl = cfg.BaseUrl.Trim().TrimEnd('/');
        if (p.Id == "custom" || (customUrl.Length > 0 && customUrl != p.DefaultBaseUrl)) Advanced.IsExpanded = true;
        _loading = was;
    }

    private void StoreProviderFields()
    {
        var p = ProviderPresets.Get(_provider);
        var cfg = _work.GetProvider(_provider);
        var url = (BaseUrl.Text ?? "").Trim().TrimEnd('/');
        cfg.BaseUrl = url == p.DefaultBaseUrl ? "" : url;
        cfg.Model = (Model.Text ?? "").Trim();
        _pendingKeys[_provider] = (ApiKey.Text ?? "").Trim();
    }

    private void StorePrompt()
    {
        var t = PromptEditor.Text ?? "";
        if (t == Prompts.GetDefault(_promptKey, _work.PromptLanguage) || string.IsNullOrWhiteSpace(t))
            _work.PromptOverrides.Remove(_promptKey);
        else
            _work.PromptOverrides[_promptKey] = t;
    }

    /// <summary>A client built from what's currently typed in the form (not yet saved).</summary>
    private IAiClient FormClient(string? model = null)
    {
        var p = ProviderPresets.Get(_provider);
        var url = (BaseUrl.Text ?? "").Trim().TrimEnd('/');
        var key = (ApiKey.Text ?? "").Trim();
        return AiClientFactory.Create(_app.Http, new AiClientOptions
        {
            ProviderId = p.Id,
            Protocol = p.Protocol,
            BaseUrl = url.Length > 0 ? url : p.DefaultBaseUrl,
            Model = model ?? (Model.Text ?? "").Trim(),
            ApiKey = key.Length > 0 ? key : SettingsStore.EnvironmentKey(p.Id),
            Temperature = DefaultTemp.IsChecked == true ? null : (double?)Temp.Value,
            Timeout = TimeSpan.FromSeconds((double)(Timeout.Value ?? 60)),
        });
    }

    private async Task RefreshModelsAsync()
    {
        TestStatus.Text = "Loading models…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var models = await FormClient().ListModelsAsync(cts.Token);
            ModelList.ItemsSource = models.ToList();
            TestStatus.Text = models.Count > 0 ? $"{models.Count} models found. Pick one from the list." : "No models were returned. Type the model name yourself.";
            if (models.Count > 0) ModelList.IsDropDownOpen = true;
        }
        catch (Exception ex) { TestStatus.Text = "Could not load models: " + ex.Message + " Check the API key and API address, or type the model name yourself."; }
    }

    private async Task TestAsync()
    {
        if (string.IsNullOrWhiteSpace(Model.Text)) { TestStatus.Text = "Type or pick a model first, then test again."; return; }
        TestStatus.Text = "Testing the connection…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var r = await FormClient().CompleteAsync(new PromptPair("You are a connection test.", "Reply with exactly: OK"), cts.Token);
            var reply = r.Text.Trim();
            TestStatus.Text = $"Works. {r.Model} replied \"{(reply.Length > 40 ? reply[..40] + "…" : reply)}\" in {r.Elapsed.TotalSeconds:0.0}s.";
        }
        catch (Exception ex) { TestStatus.Text = "Connection failed: " + ex.Message + " Check the API key, model name and API address."; }
    }

    private async Task SaveAndClose()
    {
        StoreProviderFields();
        StorePrompt();
        foreach (var (k, t) in _work.PromptOverrides)
        {
            if (k.Equals(Prompts.KeySystem, StringComparison.OrdinalIgnoreCase) ||
                k.Equals(Prompts.KeyChatSystem, StringComparison.OrdinalIgnoreCase)) continue;
            if (!t.Contains("{text}") && !t.Contains("{selectedText}") && !t.Contains("{currentResultText}"))
            {
                await Dialog.InfoAsync("Rewordly", $"The prompt \"{(Prompts.Labels.TryGetValue(k, out var l) ? l : k)}\" must contain {{selectedText}} or {{text}} where the text goes.");
                return;
            }
        }
        var active = _work.GetProvider(_provider);
        if (string.IsNullOrWhiteSpace(active.Model))
        {
            await Dialog.InfoAsync("Rewordly", "Choose a model for the selected provider: type its name, or click Refresh list and pick one.");
            return;
        }
        _work.ActiveProvider = _provider;
        AppSettings.PushRecent(active.RecentModels, active.Model, AppSettings.MaxRecentModels);
        _work.Temperature = DefaultTemp.IsChecked == true ? null : (double)(Temp.Value ?? 0.7m);
        _work.TimeoutSeconds = (int)(Timeout.Value ?? 60);
        _work.Theme = ThemeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
        _work.StartWithWindows = StartAtLogin.IsChecked == true;
        _work.TriggerDoubleCtrlC = DoubleCopy.IsChecked == true;
        _work.DoubleCopyWindowMs = (int)(WindowMs.Value ?? 800);
        _work.TriggerAltShortcut = AltShortcut.IsChecked == true;
        _work.RestoreClipboard = RestoreClip.IsChecked == true;
        _work.MaxWords = (int)(MaxWords.Value ?? 1000);

        try
        {
            foreach (var (id, key) in _pendingKeys)
            {
                var current = _app.Store.HasStoredKey(_work, id) ? _app.Store.GetApiKey(_work, id) : "";
                if ((key ?? "") != (current ?? "")) _app.Store.SetApiKey(_work, id, key);
            }
        }
        catch (Exception ex) { await Dialog.InfoAsync("Rewordly", "Could not store the API key: " + ex.Message); return; }

        _app.ApplySettings(_work);
        Toast.Show($"Saved. Using {ProviderPresets.Get(_provider).Name} · {active.Model}");
        Close();
    }
}
