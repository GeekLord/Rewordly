using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Rewordly.Desktop.Platform;

/// <summary>Clipboard access through Avalonia (Win32, NSPasteboard, X11 selections) with retries and snapshot/restore.</summary>
internal sealed class ClipboardService
{
    private readonly Func<IClipboard?> _get;

    // Formats worth restoring besides plain text. Platform-internal formats are skipped.
    private static readonly string[] Keep =
    {
        "text/html", "HTML Format", "public.html", "Rich Text Format", "public.rtf", "text/rtf",
        "image/png", "PNG", "public.png", "public.tiff", "Files", "FileNames", "text/uri-list", "public.file-url",
    };

    public ClipboardService(Func<IClipboard?> get) => _get = get;

    private IClipboard C => _get() ?? throw new InvalidOperationException("Clipboard is not available.");

    private static async Task<T?> Retry<T>(Func<Task<T?>> f, int attempts = 6)
    {
        for (int i = 0; ; i++)
        {
            try { return await f(); }
            catch (Exception ex) when (i < attempts) { if (i == 0) Log.Info("clipboard retry: " + ex.Message); await Task.Delay(50); }
            catch (Exception ex) { Log.Error("clipboard", ex); return default; }
        }
    }

    public async Task<string> GetTextAsync() => await Retry(() => C.GetTextAsync()) ?? "";

    public Task SetTextAsync(string text) => Retry<object>(async () => { await C.SetTextAsync(text); return null; });

    public Task ClearAsync() => Retry<object>(async () => { await C.ClearAsync(); return null; });

    public sealed record Snapshot(string? Text, Dictionary<string, object> Extra);

    public async Task<Snapshot?> SnapshotAsync()
    {
        try
        {
            var text = await GetTextAsync();
            var extra = new Dictionary<string, object>();
            var formats = await Retry(() => C.GetFormatsAsync()) ?? Array.Empty<string>();
            foreach (var f in formats.Where(f => Keep.Contains(f, StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    var d = await C.GetDataAsync(f);
                    if (d != null) extra[f] = d;
                }
                catch { }
            }
            return string.IsNullOrEmpty(text) && extra.Count == 0 ? null : new Snapshot(text, extra);
        }
        catch (Exception ex) { Log.Error("clip.snapshot", ex); return null; }
    }

    public async Task RestoreAsync(Snapshot? s)
    {
        try
        {
            if (s == null) { await ClearAsync(); return; }
            if (s.Extra.Count == 0) { await SetTextAsync(s.Text ?? ""); return; }
            var data = new DataObject();
            if (!string.IsNullOrEmpty(s.Text)) data.Set(DataFormats.Text, s.Text);
            foreach (var (k, v) in s.Extra) data.Set(k, v);
            await C.SetDataObjectAsync(data);
        }
        catch (Exception ex)
        {
            Log.Error("clip.restore", ex);
            if (s?.Text != null) await SetTextAsync(s.Text);
        }
    }

    /// <summary>
    /// Linux/X11 only: the current selection from the PRIMARY buffer (no copy, clipboard untouched).
    /// Needs xclip or xsel. Returns null if neither is available.
    /// </summary>
    public static string? ReadPrimarySelection()
    {
        if (!Os.IsLinux) return null;
        Os.ProcResult? r = null;
        if (Os.HasCommand("xclip")) r = Os.Run("xclip", new[] { "-o", "-selection", "primary" }, timeoutMs: 1500);
        else if (Os.HasCommand("xsel")) r = Os.Run("xsel", new[] { "--primary", "--output" }, timeoutMs: 1500);
        return r is { ExitCode: 0 } ? r.StdOut : r == null ? null : "";
    }

    /// <summary>Polls until the clipboard holds non-empty text or the timeout passes.</summary>
    public async Task<string> WaitForTextAsync(int timeoutMs)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < until)
        {
            var t = await GetTextAsync();
            if (!string.IsNullOrWhiteSpace(t)) return t;
            await Task.Delay(40);
        }
        return await GetTextAsync();
    }
}
