using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rewordly.Core;

/// <summary>Per-provider connection settings. The API key is stored encrypted.</summary>
public sealed class ProviderConfig
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string? ProtectedApiKey { get; set; }
    /// <summary>Models the user picked recently with this provider (most recent first).</summary>
    public List<string> RecentModels { get; set; } = new();
}

public sealed class AppSettings
{
    public const int MaxRecentStyles = 3;
    public const int MaxRecentLanguages = 2;
    public const int MaxRecentModels = 6;
    public const int MaxCustomStyleLength = 45;

    public int SchemaVersion { get; set; } = 1;

    // AI connection
    public string ActiveProvider { get; set; } = ProviderPresets.Gemini;
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>null = provider default.</summary>
    public double? Temperature { get; set; }
    public int TimeoutSeconds { get; set; } = 60;

    // Behaviour
    public string Theme { get; set; } = "system"; // system | light | dark
    public bool StartWithWindows { get; set; }
    public bool TriggerDoubleCtrlC { get; set; } = true;
    /// <summary>Ctrl+Insert on Windows/Linux, Ctrl+Cmd+C on macOS.</summary>
    public bool TriggerAltShortcut { get; set; } = true;
    public int DoubleCopyWindowMs { get; set; } = 800;
    public bool RestoreClipboard { get; set; } = true;
    public int MaxWords { get; set; } = 1000;
    public bool Paused { get; set; }

    // History and personalisation
    public List<string> RecentStyles { get; set; } = new();
    public List<string> RecentLanguages { get; set; } = new();
    public List<string> CustomStyles { get; set; } = new();
    public List<string> ExtraLanguages { get; set; } = new();
    public string PromptLanguage { get; set; } = "en"; // en | it | es | fr | de
    public Dictionary<string, string> PromptOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ProviderConfig GetProvider(string id)
    {
        if (!Providers.TryGetValue(id, out var cfg))
        {
            var preset = ProviderPresets.Get(id);
            cfg = new ProviderConfig { BaseUrl = preset.DefaultBaseUrl, Model = preset.DefaultModel };
            Providers[id] = cfg;
        }
        return cfg;
    }

    [JsonIgnore]
    public ProviderConfig Active => GetProvider(ActiveProvider);

    public void Normalize()
    {
        if (!ProviderPresets.All.Any(p => p.Id.Equals(ActiveProvider, StringComparison.OrdinalIgnoreCase)))
            ActiveProvider = ProviderPresets.Gemini;
        if (Providers.Comparer != StringComparer.OrdinalIgnoreCase)
            Providers = new Dictionary<string, ProviderConfig>(Providers, StringComparer.OrdinalIgnoreCase);
        if (PromptOverrides.Comparer != StringComparer.OrdinalIgnoreCase)
            PromptOverrides = new Dictionary<string, string>(PromptOverrides, StringComparer.OrdinalIgnoreCase);
        if (PromptLanguage is not ("en" or "it" or "es" or "fr" or "de"))
            PromptLanguage = "en";
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 10, 600);
        DoubleCopyWindowMs = Math.Clamp(DoubleCopyWindowMs, 200, 3000);
        MaxWords = Math.Clamp(MaxWords, 50, 20000);
        if (Temperature is { } t) Temperature = Math.Clamp(t, 0, 2);
        if (Theme is not ("system" or "light" or "dark")) Theme = "system";
        RecentStyles = RecentStyles.Where(s => !string.IsNullOrWhiteSpace(s)).Take(MaxRecentStyles).ToList();
        RecentLanguages = RecentLanguages.Where(s => !string.IsNullOrWhiteSpace(s)).Take(MaxRecentLanguages).ToList();
        CustomStyles = CustomStyles.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var p in Providers.Values)
            p.RecentModels = p.RecentModels.Where(m => !string.IsNullOrWhiteSpace(m)).Take(MaxRecentModels).ToList();
    }

    /// <summary>Moves <paramref name="item"/> to the front of <paramref name="list"/>, removing duplicates and trimming.</summary>
    public static void PushRecent(List<string> list, string item, int max)
    {
        if (string.IsNullOrWhiteSpace(item)) return;
        list.RemoveAll(s => s.Equals(item, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, item);
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
    }

    /// <summary>Adds a custom style. Returns an error message, or null on success.</summary>
    public string? AddCustomStyle(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return "Enter a style name.";
        if (name.Length > MaxCustomStyleLength) return $"Use at most {MaxCustomStyleLength} characters.";
        if (CustomStyles.Any(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            Catalog.AllStyles.Any(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return "That style already exists.";
        CustomStyles.Add(name);
        return null;
    }

    public bool RemoveCustomStyle(string name)
    {
        RecentStyles.RemoveAll(s => s.Equals(name, StringComparison.OrdinalIgnoreCase));
        return CustomStyles.RemoveAll(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
    }
}

/// <summary>
/// Stores secrets outside plain text: DPAPI on Windows, Keychain on macOS, Secret Service on Linux
/// (with an encrypted-file fallback). <paramref name="id"/> identifies the secret (e.g. provider id).
/// The returned string is what goes into settings.json (a ciphertext or a reference).
/// </summary>
public interface ISecretProtector
{
    string Protect(string id, string plain);
    string? Unprotect(string id, string storedValue);
    void Delete(string id, string storedValue) { }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ISecretProtector _protector;
    public string Folder { get; }
    public string FilePath => Path.Combine(Folder, "settings.json");

    public SettingsStore(string folder, ISecretProtector protector)
    {
        Folder = folder;
        _protector = protector;
    }

    /// <summary>Windows: %APPDATA%\Rewordly · macOS: ~/Library/Application Support/Rewordly · Linux: $XDG_CONFIG_HOME/Rewordly or ~/.config/Rewordly.</summary>
    public static string DefaultFolder
    {
        get
        {
            var overrideDir = Environment.GetEnvironmentVariable("REWORDLY_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDir)) return overrideDir;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Application Support", "Rewordly");
            if (OperatingSystem.IsWindows()) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rewordly");
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            return Path.Combine(string.IsNullOrWhiteSpace(xdg) ? Path.Combine(home, ".config") : xdg, "Rewordly");
        }
    }

    public AppSettings Load()
    {
        AppSettings s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            // Keep a copy of the unreadable file instead of silently losing it.
            try { File.Copy(FilePath, FilePath + ".corrupt", overwrite: true); } catch { }
            s = new AppSettings();
        }
        s.Normalize();
        return s;
    }

    public void Save(AppSettings s)
    {
        Directory.CreateDirectory(Folder);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Json));
        File.Move(tmp, FilePath, overwrite: true);
    }

    /// <summary>Stored key, or the provider's usual environment variable (e.g. GEMINI_API_KEY) when none is stored.</summary>
    public string? GetApiKey(AppSettings s, string providerId)
    {
        var p = s.GetProvider(providerId);
        var stored = string.IsNullOrEmpty(p.ProtectedApiKey) ? null : _protector.Unprotect(providerId.ToLowerInvariant(), p.ProtectedApiKey);
        return string.IsNullOrEmpty(stored) ? EnvironmentKey(providerId) : stored;
    }

    public bool HasStoredKey(AppSettings s, string providerId) => !string.IsNullOrEmpty(s.GetProvider(providerId).ProtectedApiKey);

    public static string? EnvironmentKey(string providerId)
    {
        foreach (var name in ProviderPresets.Get(providerId).KeyEnvironmentVariables)
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }

    public void SetApiKey(AppSettings s, string providerId, string? key)
    {
        var p = s.GetProvider(providerId);
        var id = providerId.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (!string.IsNullOrEmpty(p.ProtectedApiKey)) { try { _protector.Delete(id, p.ProtectedApiKey); } catch { } }
            p.ProtectedApiKey = null;
        }
        else p.ProtectedApiKey = _protector.Protect(id, key.Trim());
    }
}
