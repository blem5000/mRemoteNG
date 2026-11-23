using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace mRemoteNG.App.Update
{
    /// <summary>
    /// Turns the GitHub "list releases" JSON into <see cref="UpdateInfo"/>, picking the asset
    /// that matches the running build (framework-dependent or self-contained).
    /// </summary>
    public static class GitHubReleaseParser
    {
        private static readonly Regex VersionPattern = new(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Returns the newest published release that ships a matching asset, or null when none does.
        /// </summary>
        public static UpdateInfo ParseLatestRelease(string releasesJson, bool includePreReleases, string assetSuffix)
        {
            if (string.IsNullOrWhiteSpace(releasesJson) || string.IsNullOrEmpty(assetSuffix))
                return null;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(releasesJson);
            }
            catch (JsonException)
            {
                // GitHub answers with an HTML page when the API is rate limited or offline.
                return null;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (JsonElement release in document.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean())
                        continue;

                    if (release.TryGetProperty("prerelease", out JsonElement preRelease) && preRelease.GetBoolean() && !includePreReleases)
                        continue;

                    string tag = release.TryGetProperty("tag_name", out JsonElement tagElement)
                        ? tagElement.GetString() ?? string.Empty
                        : string.Empty;
                    Version version = ParseVersion(tag);
                    if (version == null) continue;

                    string changeLog = release.TryGetProperty("body", out JsonElement bodyElement)
                        ? bodyElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (JsonElement asset in assets.EnumerateArray())
                    {
                        string assetName = asset.TryGetProperty("name", out JsonElement nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty;

                        if (!assetName.EndsWith(assetSuffix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string downloadUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                            ? urlElement.GetString() ?? string.Empty
                            : string.Empty;

                        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri downloadAddress))
                            continue;
                        // Only accept HTTPS URLs on GitHub's own hosts. A tampered release JSON
                        // could otherwise point the updater at an attacker-controlled download.
                        if (!string.Equals(downloadAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string host = downloadAddress.Host.ToLowerInvariant();
                        if (!(host == "github.com" || host.EndsWith(".github.com") ||
                              host == "githubusercontent.com" || host.EndsWith(".githubusercontent.com")))
                            continue;

                        return UpdateInfo.FromGitHubRelease(tag, version, assetName, downloadAddress, changeLog,
                                                            ReadChecksum(asset));
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the version from a release tag such as "20261116-v1.78.2-NB".
        /// </summary>
        public static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;

            Match match = VersionPattern.Match(tag);
            if (!match.Success) return null;

            return new Version(int.Parse(match.Groups[1].Value),
                               int.Parse(match.Groups[2].Value),
                               int.Parse(match.Groups[3].Value));
        }

        /// <summary>
        /// GitHub reports asset hashes as "sha256:&lt;hex&gt;"; only the hex part is used.
        /// </summary>
        public static string ReadChecksum(JsonElement asset)
        {
            if (!asset.TryGetProperty("digest", out JsonElement digest) || digest.ValueKind != JsonValueKind.String)
                return string.Empty;

            string digestValue = digest.GetString() ?? string.Empty;
            int separatorIndex = digestValue.IndexOf(':');
            return separatorIndex >= 0 ? digestValue[(separatorIndex + 1)..] : digestValue;
        }
    }
}
