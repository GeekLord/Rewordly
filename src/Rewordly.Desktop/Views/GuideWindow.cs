using Avalonia;
using Avalonia.Controls;
using Rewordly.Desktop.Platform;

namespace Rewordly.Desktop.Views;

/// <summary>Short how-to, with the parts that differ per operating system.</summary>
internal sealed class GuideWindow : Window
{
    public GuideWindow(AppController app)
    {
        Ui.Prepare(this, "Rewordly – Quick guide", 620, 600);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var m = Os.ModLabel;
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(18) };
        void H(string t) => panel.Children.Add(Ui.Text(t, bold: true, size: 15));
        void P(string t) => panel.Children.Add(Ui.Text(t));

        H("1. Add an AI key (once)");
        P("Open Settings > AI model. Pick a provider (Google Gemini has a free tier), click \"Get a key\", paste it, then press Test. " +
          "Choose any model: type its name or use Refresh list. For fully offline use, install Ollama and pick \"Ollama (local, offline)\".");
        H("2. Use it anywhere");
        P($"Select text in any app and press {m}+C twice quickly while holding {m}. A menu opens at the pointer: rewrite in a style, " +
          "correct, fix errors only, summarise, explain, translate, chat, or listen.");
        P($"Alternative: {Os.AltShortcutLabel} opens the same menu without changing your clipboard.");
        H("3. Review and paste");
        P("The result window lets you edit the text, regenerate, switch model, translate the result, or listen to it. " +
          $"\"Paste and replace\" ({m}+Enter) puts it back where you copied from. Click into another window first to paste there instead.");
        H("4. Switch models quickly");
        P("Use \"Model:\" at the bottom of the action menu or in the tray/menu-bar icon. Recently used models appear there.");

        H($"Notes for {Os.Name}");
        if (Os.IsWindows)
            P("Works in normal apps. To paste into apps running as administrator, run Rewordly as administrator too. " +
              "More voices: Settings > Time & language > Speech.");
        else if (Os.IsMac)
            P("macOS asks for permission once: System Settings > Privacy & Security > Accessibility (and Input Monitoring if listed) — turn Rewordly on, then reopen it. " +
              "Rewordly lives in the menu bar, not the Dock. More voices: System Settings > Accessibility > Spoken Content.");
        else
        {
            P("Works fully in X11 sessions. On Wayland, system-wide shortcuts and pasting only work for X11/XWayland apps; choose an \"Xorg\" session at login for full support.");
            P("The tray icon needs a StatusNotifier/AppIndicator host (on GNOME install the \"AppIndicator and KStatusNotifierItem Support\" extension). " +
              "Without it, launch Rewordly again to open Settings.");
            P("Optional packages: xdotool (returns focus to the original window before pasting), libsecret-tools (stores keys in the system keyring), espeak-ng (read aloud).");
        }
        panel.Children.Add(Ui.Row(Ui.Btn("Open Settings", () => { Close(); app.ShowSettings(); }, primary: true), Ui.Btn("Close", Close)));
        Content = new ScrollViewer { Content = panel };
    }
}
