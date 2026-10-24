using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Rewordly.Core;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Free-form instruction + selected text, with optional conversation memory.</summary>
internal sealed class ChatWindow : Window
{
    private readonly AppController _app = AppController.Instance;
    private readonly WindowFocus.Target _target;
    private readonly TextBox _instruction, _text, _reply;
    private readonly Button _send;
    private readonly CheckBox _keep;
    private readonly TextBlock _status;
    private readonly List<(string Q, string A)> _history = new();

    public ChatWindow(string selectedText, WindowFocus.Target target)
    {
        _target = target;
        Ui.Prepare(this, "Rewordly – Chat", 780, 620);
        Topmost = true;

        _instruction = new TextBox
        {
            AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 70, FontSize = 14,
            Watermark = "For example: \"Reply to this email politely and say I'll call tomorrow\"",
        };
        _text = Ui.Area(TextUtil.NormalizeBlankLines(selectedText), readOnly: false);
        _reply = Ui.Area("", readOnly: false);

        _send = Ui.Btn($"Send  ({Os.ModLabel}+Enter)", SendAsync, primary: true, icon: "Send");
        var paste = Ui.Btn("Paste and replace", PasteAsync, icon: "ClipboardPaste");
        paste.IsEnabled = !target.IsEmpty;
        ToolTip.SetTip(paste, target.IsEmpty
            ? "Not available here: Chat was opened without selected text. Use Copy reply instead."
            : "Paste the reply into the window you copied from, replacing the selection.");
        ToolTip.SetShowOnDisabled(paste, true);
        var copy = Ui.Btn("Copy reply", icon: "Copy", hue: "Blue", onClick: async () => { if (!string.IsNullOrEmpty(_reply.Text)) { await _app.Clipboard.SetTextAsync(_reply.Text); _status!.Text = "Reply copied to the clipboard"; } });
        var useReply = Ui.Btn("Move reply into Text", icon: "ArrowUndo", hue: "Violet", onClick: () => { _text.Text = _reply.Text; _reply.Text = ""; _instruction.Focus(); });
        ToolTip.SetTip(useReply, "Replace the text above with this reply, so your next instruction works on it.");
        var listen = Ui.Btn("Listen", icon: "Speaker2", hue: "Teal", onClick: () => { if (!string.IsNullOrEmpty(_reply.Text)) Ui.ShowFocused(new SpeechWindow(_reply.Text)); });
        _keep = new CheckBox { Content = "Remember earlier messages", Margin = new Thickness(4, 0, 12, 6), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        ToolTip.SetTip(_keep, "Include your earlier instructions and replies in each new request. Turn off to start fresh.");
        _keep.IsCheckedChanged += (_, _) => { if (_keep.IsChecked != true) _history.Clear(); };
        _status = Ui.Text(AppController.Display(_app.Settings.Active.Model), muted: true);
        _status.FontSize = 12;
        _status.Margin = new Thickness(0, 0, 0, 4);

        var instructionLabel = Ui.Label("What should the AI do?");
        instructionLabel.Margin = new Thickness(0, 0, 0, 4);
        var replyLabel = Ui.Label("Reply");
        replyLabel.Margin = new Thickness(0, 14, 0, 4);
        _status.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
        var sendRow = Ui.Row(_send, _keep);
        var root = Ui.Rows("Auto,Auto,Auto,2*,Auto,Auto,3*,Auto",
            instructionLabel, _instruction,
            Ui.Label("Text to work on (optional)"), _text,
            sendRow,
            Ui.Split(replyLabel, _status), _reply,
            Ui.Row(paste, copy, useReply, listen));
        root.Margin = new Thickness(14, 12);
        Content = Ui.Framed(Ui.Header("Chat", "Tell the AI what to do with your text, or ask anything", "ChatSparkle", compact: true), root);

        AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            if (Ui.Mod(e) && e.Key == Key.Enter) { e.Handled = true; _ = SendAsync(); }
            else if (e.Key == Key.Escape) Close();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) => _instruction.Focus();
    }

    private async Task SendAsync()
    {
        var instruction = (_instruction.Text ?? "").Trim();
        var text = _text.Text ?? "";
        if (instruction.Length == 0 && text.Trim().Length == 0) { _instruction.Focus(); return; }
        if (instruction.Length == 0) instruction = "Respond to the text.";

        var prompt = Prompts.BuildChat(_app.Settings, instruction, text);
        if (_keep.IsChecked == true && _history.Count > 0)
        {
            var past = string.Join("\n\n", _history.Select((h, i) => $"[Earlier request {i + 1}]\n{h.Q}\n[Your earlier reply {i + 1}]\n{h.A}"));
            prompt = prompt with { User = $"Conversation so far:\n\n{past}\n\n[New request]\n{prompt.User}" };
        }
        _send.IsEnabled = false;
        _status.Text = "Waiting for the reply…";
        try
        {
            var r = await _app.RunWithBusyAsync(prompt, "Chat");
            if (r == null) return;
            var answer = TextUtil.CleanModelOutput(r.Text);
            _reply.Text = answer;
            _status.Text = $"{r.Model} · {r.Elapsed.TotalSeconds:0.0}s";
            if (_keep.IsChecked == true) _history.Add((prompt.User, answer));
        }
        finally
        {
            _send.IsEnabled = true;
            if (_status.Text == "Waiting for the reply…") _status.Text = AppController.Display(_app.Settings.Active.Model);
            Activate();
        }
    }

    private async Task PasteAsync()
    {
        if (string.IsNullOrWhiteSpace(_reply.Text)) return;
        await _app.PasteIntoAsync(_target, _reply.Text, hideFirst: this);
        Close();
    }
}
