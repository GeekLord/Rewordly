using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rewordly.Desktop.Platform;

internal sealed record Voice(string Id, string Name, string Culture)
{
    public override string ToString()
    {
        string lang;
        try { lang = CultureInfo.GetCultureInfo(Culture).DisplayName; } catch { lang = Culture; }
        return $"{Name}  ({lang})";
    }
}

/// <summary>One running utterance.</summary>
internal interface ISpeaking
{
    void Pause();
    void Resume();
    void Stop();
    event Action? Completed;
    /// <summary>Character offset and length of the word being spoken (only where the engine reports it).</summary>
    event Action<int, int>? Progress;
}

/// <summary>Offline text-to-speech: SAPI (Windows), say (macOS), espeak-ng or spd-say (Linux).</summary>
internal interface ITts
{
    string EngineName { get; }
    bool CanPause { get; }
    IReadOnlyList<Voice> Voices();
    ISpeaking Speak(string text, Voice? voice, int rate); // rate -5..5
    string MoreVoicesHint { get; }
    string? MoreVoicesLink { get; }
}

internal static class Tts
{
    public static ITts Create()
    {
        if (Os.IsWindows) return new SapiTts();
        if (Os.IsMac) return new SayTts();
        if (Os.HasCommand("espeak-ng")) return new EspeakTts("espeak-ng");
        if (Os.HasCommand("espeak")) return new EspeakTts("espeak");
        if (Os.HasCommand("spd-say")) return new SpdTts();
        return new NoTts();
    }

    public static Voice? Pick(IReadOnlyList<Voice> voices, string culture)
    {
        var lang = culture.Split('-')[0];
        return voices.FirstOrDefault(v => v.Culture.Equals(culture, StringComparison.OrdinalIgnoreCase))
               ?? voices.FirstOrDefault(v => v.Culture.Split('-', '_')[0].Equals(lang, StringComparison.OrdinalIgnoreCase));
    }
}

// ------------------------------------------------------------------------------- Windows
internal sealed class SapiTts : ITts
{
    public string EngineName => "Windows speech (SAPI)";
    public bool CanPause => true;
    public string MoreVoicesHint => "Add voices in Windows Settings > Time & language > Speech.";
    public string? MoreVoicesLink => "ms-settings:speech";

    public IReadOnlyList<Voice> Voices()
    {
        using var s = new System.Speech.Synthesis.SpeechSynthesizer();
        return s.GetInstalledVoices().Where(v => v.Enabled)
            .Select(v => new Voice(v.VoiceInfo.Name, v.VoiceInfo.Name, v.VoiceInfo.Culture.Name)).ToList();
    }

    public ISpeaking Speak(string text, Voice? voice, int rate) => new Utterance(text, voice, rate);

    private sealed class Utterance : ISpeaking
    {
        private readonly System.Speech.Synthesis.SpeechSynthesizer _s = new();
        public event Action? Completed;
        public event Action<int, int>? Progress;

        public Utterance(string text, Voice? voice, int rate)
        {
            if (voice != null) _s.SelectVoice(voice.Id);
            _s.Rate = Math.Clamp(rate * 2, -10, 10);
            _s.SpeakProgress += (_, e) => Progress?.Invoke(e.CharacterPosition, e.CharacterCount);
            _s.SpeakCompleted += (_, _) => { Completed?.Invoke(); _s.Dispose(); };
            _s.SpeakAsync(text);
        }

        public void Pause() => _s.Pause();
        public void Resume() => _s.Resume();
        public void Stop() { try { _s.Resume(); _s.SpeakAsyncCancelAll(); } catch { } }
    }
}

// ------------------------------------------------------------------------------- process-based engines
internal abstract class ProcessTts : ITts
{
    public abstract string EngineName { get; }
    public virtual bool CanPause => true;
    public abstract string MoreVoicesHint { get; }
    public virtual string? MoreVoicesLink => null;
    public abstract IReadOnlyList<Voice> Voices();
    protected abstract ProcessStartInfo Start(Voice? voice, int rate, string text, out bool textOnStdin);

    public ISpeaking Speak(string text, Voice? voice, int rate)
    {
        var psi = Start(voice, rate, text, out var stdin);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardInput = stdin;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        return new ProcUtterance(psi, stdin ? text : null);
    }

    private sealed class ProcUtterance : ISpeaking
    {
        private readonly Process _p;
        private bool _stopped;
        public event Action? Completed;
        public event Action<int, int>? Progress { add { } remove { } }

        public ProcUtterance(ProcessStartInfo psi, string? stdin)
        {
            _p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _p.Exited += (_, _) => Completed?.Invoke();
            _p.Start();
            _p.BeginOutputReadLine();
            _p.BeginErrorReadLine();
            if (stdin != null) { _p.StandardInput.Write(stdin); _p.StandardInput.Close(); }
        }

        public void Pause() { if (!_p.HasExited) Os.Pause(_p); }
        public void Resume() { if (!_p.HasExited) Os.Resume(_p); }
        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            try { if (!_p.HasExited) { Os.Resume(_p); _p.Kill(true); } } catch { }
        }
    }
}

internal sealed partial class SayTts : ProcessTts
{
    public override string EngineName => "macOS speech (say)";
    public override string MoreVoicesHint => "Add voices in System Settings > Accessibility > Spoken Content > System voice > Manage Voices.";
    public override string? MoreVoicesLink => "x-apple.systempreferences:com.apple.preference.universalaccess";

    [GeneratedRegex(@"^(?<name>.+?)\s{2,}(?<lang>[a-z]{2,3}[_-][A-Za-z0-9]+)\s+#")] private static partial Regex Line();

    public override IReadOnlyList<Voice> Voices()
    {
        var r = Os.Run("say", new[] { "-v", "?" });
        return r.StdOut.Split('\n').Select(l => Line().Match(l)).Where(m => m.Success)
            .Select(m => new Voice(m.Groups["name"].Value.Trim(), m.Groups["name"].Value.Trim(), m.Groups["lang"].Value.Replace('_', '-')))
            .ToList();
    }

    protected override ProcessStartInfo Start(Voice? voice, int rate, string text, out bool stdin)
    {
        var psi = new ProcessStartInfo("say");
        if (voice != null) { psi.ArgumentList.Add("-v"); psi.ArgumentList.Add(voice.Id); }
        psi.ArgumentList.Add("-r"); psi.ArgumentList.Add((175 + rate * 30).ToString());
        stdin = true; // with no text arguments, say reads standard input
        return psi;
    }
}

internal sealed class EspeakTts : ProcessTts
{
    private readonly string _cmd;
    public EspeakTts(string cmd) => _cmd = cmd;
    public override string EngineName => $"eSpeak NG ({_cmd})";
    public override string MoreVoicesHint => "Voices come from espeak-ng. For more natural voices install speech-dispatcher with a module such as RHVoice or Piper.";

    public override IReadOnlyList<Voice> Voices()
    {
        // Columns: Pty Language Age/Gender VoiceName File Other
        var r = Os.Run(_cmd, new[] { "--voices" });
        var list = new List<Voice>();
        foreach (var line in r.StdOut.Split('\n').Skip(1))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            var lang = parts[1];
            list.Add(new Voice(lang, parts[3].Replace('_', ' '), lang));
        }
        return list.GroupBy(v => v.Id).Select(g => g.First()).ToList();
    }

    protected override ProcessStartInfo Start(Voice? voice, int rate, string text, out bool stdin)
    {
        var psi = new ProcessStartInfo(_cmd);
        if (voice != null) { psi.ArgumentList.Add("-v"); psi.ArgumentList.Add(voice.Id); }
        psi.ArgumentList.Add("-s"); psi.ArgumentList.Add((170 + rate * 25).ToString());
        psi.ArgumentList.Add("--stdin");
        stdin = true;
        return psi;
    }
}

internal sealed class SpdTts : ProcessTts
{
    public override string EngineName => "speech-dispatcher (spd-say)";
    public override bool CanPause => false;
    public override string MoreVoicesHint => "Voices come from speech-dispatcher modules (espeak-ng, RHVoice, Piper…).";

    public override IReadOnlyList<Voice> Voices() => new[] { "en-US", "hi-IN", "de-DE", "fr-FR", "es-ES" }
        .Select(c => new Voice(c, "Default", c)).ToList();

    protected override ProcessStartInfo Start(Voice? voice, int rate, string text, out bool stdin)
    {
        var psi = new ProcessStartInfo("spd-say");
        psi.ArgumentList.Add("-w");
        psi.ArgumentList.Add("-r"); psi.ArgumentList.Add((rate * 15).ToString());
        if (voice != null) { psi.ArgumentList.Add("-l"); psi.ArgumentList.Add(voice.Culture.Split('-')[0]); }
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(text);
        stdin = false;
        return psi;
    }
}

internal sealed class NoTts : ITts
{
    public string EngineName => "none";
    public bool CanPause => false;
    public string MoreVoicesHint => "No speech engine found. Install espeak-ng (e.g. sudo apt install espeak-ng) and reopen this window.";
    public string? MoreVoicesLink => null;
    public IReadOnlyList<Voice> Voices() => Array.Empty<Voice>();
    public ISpeaking Speak(string text, Voice? voice, int rate) => throw new InvalidOperationException(MoreVoicesHint);
}
