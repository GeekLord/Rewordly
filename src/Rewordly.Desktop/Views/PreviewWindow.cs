using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Rewordly.Core;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Result next to the original, with paste, copy, regenerate, translate and listen.</summary>
internal sealed class PreviewWindow : Window
{
    private readonly AppController _app = AppController.Instance;
    private readonly string _original;
    private readonly PromptPair _prompt;
    private readonly WindowFocus.Target _target;
    private WindowFocus.Target _pasteTarget;
    private bool _keepOpenAfterPaste;
    private string _baseResult;

    internal readonly TextBox Original, Result;
    internal readonly CheckBox ShowOriginal;
    internal readonly Button PasteButton, CopyButton, RegenButton, BackButton;
    internal readonly DropDownButton TranslateButton, ListenButton;
    internal readonly ComboBox ModelBox;
    internal readonly TextBlock Status;

    public PreviewWindow(ActionSpec action, string original, string result, PromptPair prompt, WindowFocus.Target target, AiResult info)
    {
        _original = original; _prompt = prompt; _target = target; _pasteTarget = target; _baseResult = result;
        Ui.Prepare(this, $"Rewordly – {action.Title}", 740, 540);
        Topmost = true;

        Original = Ui.Area(TextUtil.NormalizeBlankLines(original), readOnly: true);
        Result = Ui.Area(result, readOnly: false);
        Original.IsVisible = false;

        ShowOriginal = new CheckBox { Content = "Show original", Margin = new Thickness(0, 0, 12, 6) };
        ShowOriginal.IsCheckedChanged += (_, _) => Original.IsVisible = ShowOriginal.IsChecked == true;

        PasteButton = Ui.Btn("Paste and replace", PasteAsync, primary: true);
        CopyButton = Ui.Btn("Copy", CopyAsync);
        RegenButton = Ui.Btn("Regenerate", RegenerateAsync);
        Status = Ui.Text($"{info.Elapsed.TotalSeconds:0.0}s · {info.Model}", muted: true);
        Status.Margin = new Thickness(0, 0, 0, 4);
        Status.FontSize = 12;

        var flyout = new MenuFlyout();
        flyout.ItemsSource = MenuFactory.TranslateItems(_app, lang => _ = TranslateAsync(lang), () => flyout.Hide());
        TranslateButton = new DropDownButton { Content = "Translate result", Flyout = flyout, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 5) };
        BackButton = Ui.Btn("Undo translation", () => { Result.Text = _baseResult; BackButton!.IsVisible = false; Status.Text = "Showing the untranslated result"; });
        BackButton.IsVisible = false;
        ToolTip.SetTip(BackButton, "Go back to the result before it was translated.");

        var listenMenu = new MenuFlyout();
        var listenRes = new MenuItem { Header = "Result" };
        listenRes.Click += (_, _) => Ui.ShowFocused(new SpeechWindow(Result.Text ?? ""));
        var listenOrig = new MenuItem { Header = "Original text" };
        listenOrig.Click += (_, _) => Ui.ShowFocused(new SpeechWindow(_original));
        listenMenu.ItemsSource = new List<Control> { listenRes, listenOrig };
        ListenButton = new DropDownButton { Content = "Listen", Flyout = listenMenu, Margin = new Thickness(0, 0, 8, 6), Padding = new Thickness(12, 5) };
        ToolTip.SetTip(ListenButton, "Read the result or the original aloud.");

        var cfg = _app.Settings.Active;
        ModelBox = new ComboBox { MinWidth = 200, Margin = new Thickness(0, 0, 8, 6) };
        ModelBox.ItemsSource = new[] { cfg.Model }.Concat(cfg.RecentModels).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        ModelBox.SelectedIndex = 0;
        ToolTip.SetTip(ModelBox, "Model used by Regenerate and Translate result.");
        var m = Os.ModLabel;
        ToolTip.SetTip(PasteButton, $"Paste into the window you copied from, replacing the selection ({m}+Enter). Click into another window first to paste there instead.");
        ToolTip.SetTip(CopyButton, "Copy the result to the clipboard without pasting it.");
        ToolTip.SetTip(RegenButton, $"Ask the AI again with the model chosen below ({m}+R).");

        var originalLabel = Ui.Label("Original");
        originalLabel.Margin = new Thickness(0, 0, 0, 4);
        var resultLabel = Ui.Label("Result (you can edit it before pasting)");
        resultLabel.Margin = new Thickness(0, 0, 0, 4);
        var resultHead = Ui.Split(resultLabel, Status);
        var texts = new Grid { RowDefinitions = RowDefinitions.Parse("Auto,Auto,Auto,*"), Margin = new Thickness(0, 0, 0, 6) };
        Original.MaxHeight = 160;
        Original.Margin = new Thickness(0, 0, 0, 12);
        originalLabel.IsVisible = false;
        ShowOriginal.IsCheckedChanged += (_, _) => originalLabel.IsVisible = ShowOriginal.IsChecked == true;
        Grid.SetRow(Original, 1); Grid.SetRow(resultHead, 2); Grid.SetRow(Result, 3);
        texts.Children.Add(originalLabel);
        texts.Children.Add(Original);
        texts.Children.Add(resultHead);
        texts.Children.Add(Result);

        // Primary row: what to do with the result. Secondary row: ways to change or inspect it.
        var top = Ui.Row(PasteButton, CopyButton, RegenButton, TranslateButton, BackButton);
        var modelLabel = Ui.Text("Model for Regenerate and Translate:", muted: true);
        modelLabel.FontSize = 12;
        var bottom = Ui.Row(ShowOriginal, ListenButton, modelLabel, ModelBox);
        var keys = Ui.Hint($"{m}+Enter paste and replace  ·  {m}+R regenerate  ·  Esc close");
        keys.Margin = new Thickness(0, 2, 0, 0);
        var root = Ui.Rows("*,Auto,Auto,Auto", texts, top, bottom, keys);
        root.Margin = new Thickness(12);
        Content = root;

        AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (Ui.Mod(e) && e.Key == Key.Enter) { e.Handled = true; _ = PasteAsync(); }
            else if (Ui.Mod(e) && e.Key == Key.R) { e.Handled = true; _ = RegenerateAsync(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Deactivated += (_, _) => DispatcherTimer.RunOnce(TrackOtherWindow, TimeSpan.FromMilliseconds(150));
        Opened += (_, _) => { Result.Focus(); Result.CaretIndex = 0; };
    }

    private string ChosenModel => ModelBox.SelectedItem as string ?? _app.Settings.Active.Model;

    // If the user clicks into another window, paste there and keep this window open.
    private void TrackOtherWindow()
    {
        if (!IsVisible) return;
        var fg = WindowFocus.Capture();
        if (fg.IsEmpty || WindowFocus.IsOwn(fg) || fg == _target) return;
        _pasteTarget = fg;
        _keepOpenAfterPaste = true;
        PasteButton.Content = "Paste into other window";
        ToolTip.SetTip(PasteButton, "Paste into the window you last clicked. Rewordly stays open so you can paste again.");
    }

    private async Task PasteAsync()
    {
        var text = Result.Text ?? "";
        if (string.IsNullOrWhiteSpace(text)) { await Dialog.InfoAsync("Rewordly", "There is no text to paste."); return; }
        await _app.PasteIntoAsync(_pasteTarget, text, hideFirst: this);
        if (_keepOpenAfterPaste) Show(); else Close();
    }

    private async Task CopyAsync()
    {
        if (string.IsNullOrWhiteSpace(Result.Text)) return;
        await _app.Clipboard.SetTextAsync(Result.Text);
        Status.Text = "Copied to the clipboard";
    }

    private void SetBusy(bool busy)
    {
        PasteButton.IsEnabled = CopyButton.IsEnabled = RegenButton.IsEnabled = TranslateButton.IsEnabled = ModelBox.IsEnabled = !busy;
        Result.IsReadOnly = busy;
    }

    private async Task RegenerateAsync()
    {
        SetBusy(true);
        try
        {
            var r = await _app.RunWithBusyAsync(_prompt, "Regenerating", ChosenModel);
            if (r == null) return;
            _baseResult = TextUtil.CleanModelOutput(r.Text, _original);
            Result.Text = _baseResult;
            BackButton.IsVisible = false;
            Status.Text = $"{r.Elapsed.TotalSeconds:0.0}s · {r.Model}";
            RememberModel();
        }
        finally { SetBusy(false); Activate(); }
    }

    private async Task TranslateAsync(string lang)
    {
        var source = Result.Text ?? "";
        if (string.IsNullOrWhiteSpace(source)) return;
        AppSettings.PushRecent(_app.Settings.RecentLanguages, lang, AppSettings.MaxRecentLanguages);
        _app.SaveSettings();
        SetBusy(true);
        try
        {
            var prompt = Prompts.BuildRetranslate(_app.Settings, lang, source);
            var r = await _app.RunWithBusyAsync(prompt, $"Translating into {lang}", ChosenModel);
            if (r == null) return;
            Result.Text = TextUtil.CleanModelOutput(r.Text, source);
            BackButton.IsVisible = true;
            Status.Text = $"{r.Elapsed.TotalSeconds:0.0}s · {lang}";
        }
        finally { SetBusy(false); Activate(); }
    }

    private void RememberModel()
    {
        var cfg = _app.Settings.Active;
        if (ChosenModel != cfg.Model)
        {
            AppSettings.PushRecent(cfg.RecentModels, ChosenModel, AppSettings.MaxRecentModels);
            _app.SaveSettings();
            _app.RebuildTrayMenu();
        }
    }
}
