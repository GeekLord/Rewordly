# Troubleshooting

## The menu does not open

- **Check that shortcuts are not paused:** open the tray menu and make sure **Pause shortcuts** is not ticked.
- **Press faster, or allow more time:** the two copy presses must come within the interval set in **Settings › Shortcuts & behaviour** (800 ms by default). Keep `Ctrl` (or `Cmd`) held down between the presses.
- **Try the alternative shortcut:** `Ctrl+Insert` (Windows), `Ctrl+Cmd+C` (macOS) or `Ctrl+Alt+C` (Linux).

### Windows: not in apps running as administrator

Windows blocks normal apps from reading keys in elevated windows. Run `Rewordly.exe` as administrator too, or use Rewordly in non-elevated apps.

### macOS: nothing happens

Rewordly needs **Accessibility** (and on some versions **Input Monitoring**) permission. Open **System Settings › Privacy & Security › Accessibility**, remove Rewordly if it is listed, add `Rewordly.app` again and switch it on. Then quit and restart Rewordly.

### Linux: shortcuts don't work under Wayland

Wayland does not let apps listen to keys system-wide. Shortcuts work only in X11/XWayland windows. For full support, choose an X11 (Xorg) session on the login screen. The alternative shortcut needs `xclip` or `xsel`, and `xdotool` helps Rewordly paste back into the right window.

## The AI does not answer

- Click **Test connection** in **Settings › AI model**. The message says what went wrong: a refused key, an unknown model, a blocked request or a timeout.
- **"API key was refused":** create a new key with **Get a key** and paste it again.
- **"Model was not found":** click **Refresh list** and pick a model your key can use.
- **Timeouts with large texts or local models:** raise **Timeout** under **Advanced** on the AI model tab.
- **Ollama:** make sure Ollama is running (`ollama list` in a terminal) and that you pulled the model.

## The result has extra text or the model ignores instructions

Small models sometimes add a line such as "Here is the rewritten text:" or invent details. Try **Regenerate**, pick a larger model, or tighten the prompt under **Settings › Prompts**.

## "The selection has … words; the limit is …"

Your selection has more words than **Maximum words per selection** in **Settings › Shortcuts & behaviour**. Select less text or raise the limit.

## Paste and replace pastes in the wrong place

Rewordly pastes into the window you copied from. If you clicked elsewhere in between, click back into the original text field first, or use **Copy** and paste yourself.

## Listen has no voice or the wrong language

- **Linux:** install `espeak-ng` (`sudo apt install espeak-ng`).
- **All systems:** click **Get more voices** in the Listen window. On Windows and macOS it opens the system voice settings; on Linux it explains which voice packages to install. Then pick a voice under **Voice**.

## Still stuck?

Open an issue on [GitHub](https://github.com/GeekLord/Rewordly/issues) with your system, Rewordly version (**Settings › About**) and what you tried. Report security problems privately as described in the [security policy](../SECURITY.md).
