using System.Security.Cryptography;
using System.Text;
using Rewordly.Core;

namespace Rewordly.Desktop.Platform;

/// <summary>
/// Picks the best secret store for the OS. Stored values carry a scheme prefix so the right
/// backend is used to read them back:
///   dpapi:…        Windows DPAPI (current user)
///   keychain:…     macOS Keychain (reference only; the key lives in the Keychain)
///   secret-tool:…  Linux Secret Service / GNOME Keyring / KWallet (reference only)
///   aes:…          AES-GCM with a random per-user key file (0600) — fallback when no keyring exists
/// </summary>
internal sealed class SecretStore : ISecretProtector
{
    private const string Service = "Rewordly";
    private readonly string _folder;

    public SecretStore(string folder) => _folder = folder;

    public string Backend =>
        Os.IsWindows ? "Windows DPAPI" :
        Os.IsMac ? "macOS Keychain" :
        Os.HasCommand("secret-tool") ? "Secret Service (keyring)" : "encrypted file (install libsecret-tools for keyring storage)";

    public string Protect(string id, string plain)
    {
        if (Os.IsWindows) return "dpapi:" + Dpapi.Protect(plain);
        if (Os.IsMac && Keychain.Set(id, plain)) return "keychain:" + id;
        if (Os.IsLinux && Os.HasCommand("secret-tool") && SecretTool.Set(id, plain)) return "secret-tool:" + id;
        return "aes:" + FileKey.Encrypt(_folder, plain);
    }

    public string? Unprotect(string id, string stored)
    {
        try
        {
            var i = stored.IndexOf(':');
            if (i < 0) return null;
            var scheme = stored[..i];
            var value = stored[(i + 1)..];
            return scheme switch
            {
                "dpapi" when Os.IsWindows => Dpapi.Unprotect(value),
                "keychain" when Os.IsMac => Keychain.Get(value),
                "secret-tool" when Os.IsLinux => SecretTool.Get(value),
                "aes" => FileKey.Decrypt(_folder, value),
                _ => null,
            };
        }
        catch (Exception ex) { Log.Error("secret", ex); return null; }
    }

    public void Delete(string id, string stored)
    {
        if (stored.StartsWith("keychain:") && Os.IsMac) Keychain.Delete(stored[9..]);
        if (stored.StartsWith("secret-tool:") && Os.IsLinux) SecretTool.Delete(stored[12..]);
    }

    private static class Dpapi
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Rewordly.v1.apikey");
        public static string Protect(string plain) =>
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));
        public static string Unprotect(string v) =>
            Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), Entropy, DataProtectionScope.CurrentUser));
    }

    private static class Keychain
    {
        private const string Tool = "/usr/bin/security";
        public static bool Set(string account, string secret) =>
            Os.Run(Tool, new[] { "add-generic-password", "-U", "-s", Service, "-a", account, "-l", "Rewordly API key", "-w", secret }).ExitCode == 0;
        public static string? Get(string account)
        {
            var r = Os.Run(Tool, new[] { "find-generic-password", "-s", Service, "-a", account, "-w" });
            return r.ExitCode == 0 ? r.StdOut.TrimEnd('\n', '\r') : null;
        }
        public static void Delete(string account) => Os.Run(Tool, new[] { "delete-generic-password", "-s", Service, "-a", account });
    }

    private static class SecretTool
    {
        public static bool Set(string account, string secret) =>
            Os.Run("secret-tool", new[] { "store", "--label=Rewordly API key", "service", Service, "account", account }, stdin: secret).ExitCode == 0;
        public static string? Get(string account)
        {
            var r = Os.Run("secret-tool", new[] { "lookup", "service", Service, "account", account });
            return r.ExitCode == 0 && r.StdOut.Length > 0 ? r.StdOut.TrimEnd('\n') : null;
        }
        public static void Delete(string account) => Os.Run("secret-tool", new[] { "clear", "service", Service, "account", account });
    }

    /// <summary>AES-256-GCM with a random key stored in a user-only file next to the settings.</summary>
    internal static class FileKey
    {
        private static byte[] Key(string folder)
        {
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, ".secret.key");
            if (File.Exists(path))
            {
                var k = File.ReadAllBytes(path);
                if (k.Length == 32) return k;
            }
            var key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(path, key);
            Os.MakePrivate(path);
            return key;
        }

        public static string Encrypt(string folder, string plain)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var data = Encoding.UTF8.GetBytes(plain);
            var cipher = new byte[data.Length];
            var tag = new byte[16];
            using var aes = new AesGcm(Key(folder), 16);
            aes.Encrypt(nonce, data, cipher, tag);
            return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
        }

        public static string Decrypt(string folder, string value)
        {
            var all = Convert.FromBase64String(value);
            var nonce = all[..12]; var tag = all[12..28]; var cipher = all[28..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(Key(folder), 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
