using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Rewordly.Core;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Offline read-aloud with the system's own voices.</summary>
internal sealed class SpeechWindow : Window
{
    private readonly ITts _tts = AppController.Instance.Tts;
    private readonly TextBox _box;
    private readonly ComboBox _voices;
    private readonly Slider _rate;
    private readonly Button _play;
    private readonly TextBlock _info;
    private ISpeaking? _speaking;
    private bool _paused;

    public SpeechWindow(string text)
    {
        Ui.Prepare(this, "Rewordly – Listen", 640, 440);
        _box = Ui.Area(TextUtil.NormalizeBlankLines(text), readOnly: true);
        _voices = new ComboBox { MinWidth = 300, Margin = new Thickness(0, 0, 8, 6) };
        _rate = new Slider { Minimum = -5, Maximum = 5, Value = 0, Width = 160, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 12, 0) };
        _play = Ui.Btn("Play", PlayPause, primary: true);
        var stop = Ui.Btn("Stop", Stop);
        _info = Ui.Text("", muted: true);
        var more = Ui.Btn("Get more voices", () => { if (_tts.MoreVoicesLink != null) Os.Open(_tts.MoreVoicesLink); else _info.Text = _tts.MoreVoicesHint; });
        ToolTip.SetTip(more, _tts.MoreVoicesHint);

        IReadOnlyList<Voice> voices = Array.Empty<Voice>();
        try { voices = _tts.Voices(); } catch (Exception ex) { Log.Error("tts.voices", ex); }
        _voices.ItemsSource = voices;
        var culture = LanguageGuess.GuessCulture(text);
        var pick = Tts.Pick(voices, culture);
        if (pick != null) _voices.SelectedItem = pick;
        else if (voices.Count > 0) { _voices.SelectedIndex = 0; _info.Text = $"No voice for {culture} found; using {voices[0]}."; }
        if (voices.Count == 0) { _play.IsEnabled = false; _info.Text = _tts.MoreVoicesHint; }

        var root = Ui.Rows("*,Auto,Auto,Auto", _box,
            Ui.Row(_play, stop, Ui.Text("Voice:", muted: true), _voices),
            Ui.Row(Ui.Text("Speed:", muted: true), _rate, more),
            Ui.Row(Ui.Text(_tts.EngineName, muted: true), _info));
        root.Margin = new Thickness(12);
        Content = root;

        AddHandler(KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Space) { e.Handled = true; PlayPause(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Closed += (_, _) => _speaking?.Stop();
        Opened += (_, _) => { if (_play.IsEnabled) PlayPause(); };
    }

    private void PlayPause()
    {
        try
        {
            if (_speaking == null)
            {
                var s = _tts.Speak(_box.Text ?? "", _voices.SelectedItem as Voice, (int)_rate.Value);
                s.Completed += () => Dispatcher.UIThread.Post(Finished);
                s.Progress += (pos, len) => Dispatcher.UIThread.Post(() => Highlight(pos, len));
                _speaking = s; _paused = false; _play.Content = _tts.CanPause ? "Pause" : "Playing…";
            }
            else if (!_tts.CanPause) return;
            else if (_paused) { _speaking.Resume(); _paused = false; _play.Content = "Pause"; }
            else { _speaking.Pause(); _paused = true; _play.Content = "Resume"; }
        }
        catch (Exception ex) { Log.Error("tts", ex); _info.Text = "Could not start speech: " + ex.Message; _speaking = null; }
    }

    private void Stop()
    {
        _speaking?.Stop();
        Finished();
    }

    private void Finished()
    {
        _speaking = null; _paused = false; _play.Content = "Play";
        _box.SelectionStart = _box.SelectionEnd = 0;
    }

    private void Highlight(int start, int length)
    {
        var t = _box.Text ?? "";
        if (start < 0 || start + length > t.Length) return;
        _box.Focus();
        _box.SelectionStart = start;
        _box.SelectionEnd = start + length;
    }
}
