using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace mRemoteNG.Plugins.SshTransfer;

/// <summary>
/// A minimal known-hosts store for the SSH transfer plugin. SSH.NET accepts any host key by
/// default, so without this a network attacker could impersonate the server and capture the
/// connection's credentials and the transferred file.
///
/// Format: one "host:port sha256:&lt;fingerprint&gt;" entry per line. First use trusts the key
/// (TOFU); a changed key is rejected until the user removes the stale entry.
/// </summary>
internal static class KnownHostsStore
{
    private static readonly string DefaultStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "mRemoteNG", "ssh_known_hosts");

    // Test seam: tests point this at a temporary file so they never touch the user's real store.
    internal static string StorePath = DefaultStorePath;

    private static readonly object SyncRoot = new();

    internal enum TrustResult { Trusted, FirstUse, Changed }

    internal static TrustResult CheckOrRemember(string host, int port, string fingerprintSha256)
    {
        string key = $"{host}:{port}";
        string entry = $"{key} sha256:{fingerprintSha256}";
        lock (SyncRoot)
        {
            Dictionary<string, string> entries = Read();
            if (entries.TryGetValue(key, out string? known))
                return string.Equals(known, fingerprintSha256, StringComparison.OrdinalIgnoreCase)
                    ? TrustResult.Trusted
                    : TrustResult.Changed;

            entries[key] = fingerprintSha256;
            Write(entries);
            return TrustResult.FirstUse;
        }
    }

    private static Dictionary<string, string> Read()
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(StorePath))
            return result;

        foreach (string line in File.ReadAllLines(StorePath))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[1].StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                result[parts[0]] = parts[1][7..];
        }
        return result;
    }

    private static void Write(Dictionary<string, string> entries)
    {
        string? directory = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(StorePath, entries
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key} sha256:{p.Value}"));
    }
}
