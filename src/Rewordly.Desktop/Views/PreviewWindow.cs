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
    internal readonly Button PasteButton, CopyButton, RegenButton, TranslateButton, BackButton;
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
        TranslateButton = Ui.Btn("Translate result ▾", () => { });
        var flyout = new MenuFlyout();
        flyout.ItemsSource = MenuFactory.TranslateItems(_app, lang => _ = TranslateAsync(lang), () => flyout.Hide());
        TranslateButton.Flyout = flyout;
        BackButton = Ui.Btn("Back to result", () => { Result.Text = _baseResult; BackButton!.IsVisible = false; });
        BackButton.IsVisible = false;
        var listenOrig = Ui.Btn("Listen: original", () => Ui.ShowFocused(new SpeechWindow(_original)));
        var listenRes = Ui.Btn("Listen: result", () => Ui.ShowFocused(new SpeechWindow(Result.Text ?? "")));

        var cfg = _app.Settings.Active;
        ModelBox = new ComboBox { MinWidth = 220, Margin = new Thickness(0, 0, 8, 6) };
        ModelBox.ItemsSource = new[] { cfg.Model }.Concat(cfg.RecentModels).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();
        ModelBox.SelectedIndex = 0;
        ToolTip.SetTip(ModelBox, "Model used by Regenerate and Translate result.");
        ToolTip.SetTip(PasteButton, "Paste into the window you copied from, replacing the selection. Click into another window first to paste there instead.");
        Status = Ui.Text($"{info.Elapsed.TotalSeconds:0.0}s", muted: true);

        var texts = new Grid { RowDefinitions = RowDefinitions.Parse("Auto,*"), Margin = new Thickness(0, 0, 0, 4) };
        Original.MaxHeight = 220;
        Original.Margin = new Thickness(0, 0, 0, 8);
        Grid.SetRow(Result, 1);
        texts.Children.Add(Original);
        texts.Children.Add(Result);

        var top = Ui.Row(PasteButton, CopyButton, RegenButton, TranslateButton, BackButton);
        var bottom = Ui.Row(ShowOriginal, listenOrig, listenRes, Ui.Text("Model:", muted: true), ModelBox, Status);
        var root = Ui.Rows("*,Auto,Auto", texts, top, bottom);
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
        PasteButton.Content = "Paste";
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
        Status.Text = "Copied to clipboard";
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
