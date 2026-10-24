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
    private string _promptKey = Prompts.KeyStyle;

    public SettingsWindow(AppController app)
    {
        _app = app;
        _work = Clone(app.Settings);
        _provider = ProviderPresets.Get(_work.ActiveProvider).Id;
        Ui.Prepare(this, "Rewordly – Settings", 760, 640);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // ---------------------------------------------------------------- AI model tab
        ProviderBox = new ComboBox { ItemsSource = ProviderPresets.All.Select(p => p.Name).ToList(), MinWidth = 320 };
        ProviderNotes = Ui.Text("", muted: true);
        BaseUrl = new TextBox { MinWidth = 420 };
        ApiKey = new TextBox { MinWidth = 420, PasswordChar = '•' };
        ShowKey = new CheckBox { Content = "Show", Margin = new Thickness(8, 0, 0, 0) };
        ShowKey.IsCheckedChanged += (_, _) => ApiKey.PasswordChar = ShowKey.IsChecked == true ? '\0' : '•';
        var getKey = Ui.Btn("Get a key", () => { var u = ProviderPresets.Get(_provider).KeyHelpUrl; if (u.Length > 0) Os.Open(u); });
        KeyInfo = Ui.Text("", muted: true);
        Model = new TextBox { MinWidth = 320 };
        ModelList = new ComboBox { MinWidth = 260, PlaceholderText = "Available models (Refresh list)" };
        ModelList.SelectionChanged += (_, _) => { if (ModelList.SelectedItem is string m) Model.Text = m; };
        var refresh = Ui.Btn("Refresh list", RefreshModelsAsync);
        var test = Ui.Btn("Test", TestAsync);
        TestStatus = Ui.Text("", muted: true);
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

        var ai = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Ui.Text("Provider", bold: true), ProviderBox, ProviderNotes,
                Ui.Text("API address", bold: true), BaseUrl,
                Ui.Text("API key", bold: true), Ui.Row(ApiKey, ShowKey), Ui.Row(getKey), KeyInfo,
                Ui.Text("Model", bold: true), Ui.Row(Model, ModelList), Ui.Row(refresh, test, TestStatus),
                Ui.Text("Temperature", bold: true), Ui.Row(DefaultTemp, Temp),
                Ui.Text("Timeout (seconds)", bold: true), Timeout,
            },
        };

        // ---------------------------------------------------------------- behaviour tab
        ThemeBox = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, Width = 160 };
        StartAtLogin = new CheckBox { Content = $"Start Rewordly when I log in to {Os.Name}" };
        DoubleCopy = new CheckBox { Content = $"Open the menu when I press {Os.ModLabel}+C twice (hold {Os.ModLabel})" };
        WindowMs = new NumericUpDown { Minimum = 250, Maximum = 2000, Increment = 50, FormatString = "0", Width = 140 };
        AltShortcut = new CheckBox { Content = $"Also open the menu with {Os.AltShortcutLabel} (doesn't touch your clipboard)" };
        RestoreClip = new CheckBox { Content = "Put my previous clipboard back after copying and pasting" };
        MaxWords = new NumericUpDown { Minimum = 50, Maximum = 20000, Increment = 100, FormatString = "0", Width = 140 };
        var behaviour = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Text("Theme", bold: true), ThemeBox,
                StartAtLogin, DoubleCopy,
                Ui.Row(Ui.Text("Time allowed between the two presses (ms):"), WindowMs),
                AltShortcut, RestoreClip,
                Ui.Row(Ui.Text("Maximum words per selection:"), MaxWords),
                Ui.Text(Os.IsWayland
                    ? "Wayland session detected: system-wide shortcuts only work in X11/XWayland apps."
                    : Os.IsMac ? "macOS: Rewordly must be allowed under Privacy & Security > Accessibility." : "", muted: true),
            },
        };

        // ---------------------------------------------------------------- prompts tab
        var keys = Prompts.Labels.Keys.ToList();
        PromptKeyBox = new ComboBox { ItemsSource = keys.Select(k => Prompts.Labels[k]).ToList(), MinWidth = 320 };
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
        var reset = Ui.Btn("Reset to default", () =>
        {
            _work.PromptOverrides.Remove(_promptKey);
            PromptEditor.Text = Prompts.GetDefault(_promptKey, _work.PromptLanguage);
        });
        var prompts = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Row(Ui.Text("Template language:"), PromptLanguageBox),
                Ui.Text("Prompt", bold: true), PromptKeyBox, PromptEditor, Ui.Row(reset),
                Ui.Text("Placeholders: {selectedText} or {text}, {selectedStyle}, {num}, {value}, {currentResultText}.", muted: true),
            },
        };

        // ---------------------------------------------------------------- about tab
        var about = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Ui.Text("Rewordly 2.0.0", bold: true, size: 18),
                Ui.Text("Rewrite, fix, summarise, explain, translate, chat about and listen to any selected text, in any app, on Windows, macOS and Linux."),
                Ui.Text($"System: {Os.Name}{(Os.IsWayland ? " (Wayland)" : "")} · Speech: {_app.Tts.EngineName} · Key storage: {_app.Secrets.Backend}", muted: true),
                Ui.Text($"Settings folder: {_app.Store.Folder}", muted: true),
                Ui.Row(Ui.Btn("Open settings folder", () => { Directory.CreateDirectory(_app.Store.Folder); Os.Open(_app.Store.Folder); }), Ui.Btn("Quick guide", _app.ShowGuide)),
                Ui.Text("Your text is sent only to the AI provider you choose. Nothing is sent anywhere else.", muted: true),
            },
        };

        Control Pad(Control c) => new ScrollViewer { Content = new Border { Padding = new Thickness(14), Child = c } };
        var tabs = new TabControl
        {
            Padding = new Thickness(8, 0),
            ItemsSource = new[]
            {
                new TabItem { FontSize = 16, Header = "AI model", Content = Pad(ai) },
                new TabItem { FontSize = 16, Header = "Shortcuts & behaviour", Content = Pad(behaviour) },
                new TabItem { FontSize = 16, Header = "Prompts", Content = Pad(prompts) },
                new TabItem { FontSize = 16, Header = "About", Content = Pad(about) },
            },
        };
        var save = Ui.Btn("Save", SaveAndClose, primary: true);
        var cancel = Ui.Btn("Cancel", Close);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 4, 4, 8), Children = { save, cancel } };
        Content = Ui.Rows("*,Auto", tabs, buttons);

        LoadAll();
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
        KeyInfo.Text = (p.NeedsKey ? "Required. " : "Not required. ") + $"Stored with: {_app.Secrets.Backend}." +
                       (env != null ? $" If left empty, the {env} environment variable is used." :
                        p.KeyEnvironmentVariables.Length > 0 ? $" You can also set {string.Join(" or ", p.KeyEnvironmentVariables)} instead." : "");
        Model.Text = cfg.Model.Length > 0 ? cfg.Model : p.DefaultModel;
        ModelList.ItemsSource = cfg.RecentModels.ToList();
        TestStatus.Text = "";
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
            TestStatus.Text = $"{models.Count} models found. Pick one from the list.";
            if (models.Count > 0) ModelList.IsDropDownOpen = true;
        }
        catch (Exception ex) { TestStatus.Text = "Could not load models: " + ex.Message; }
    }

    private async Task TestAsync()
    {
        if (string.IsNullOrWhiteSpace(Model.Text)) { TestStatus.Text = "Enter or pick a model first."; return; }
        TestStatus.Text = "Testing…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var r = await FormClient().CompleteAsync(new PromptPair("You are a connection test.", "Reply with exactly: OK"), cts.Token);
            var reply = r.Text.Trim();
            TestStatus.Text = $"Works. {r.Model} replied \"{(reply.Length > 40 ? reply[..40] + "…" : reply)}\" in {r.Elapsed.TotalSeconds:0.0}s.";
        }
        catch (Exception ex) { TestStatus.Text = "Failed: " + ex.Message; }
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
            await Dialog.InfoAsync("Rewordly", "Choose a model for the selected provider (type a name or use Refresh list).");
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
