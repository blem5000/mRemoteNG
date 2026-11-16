using System;
using System.Text.RegularExpressions;

namespace mRemoteNG.App.Update
{
    /// <summary>
    /// Compares fork release tags such as <c>20261116b-v1.78.2-NB</c>.
    ///
    /// Every release keeps the application version (1.78.2) and only the tag changes, so the
    /// tag — not the version — decides whether a build is newer. The tag starts with a date
    /// (yyyyMMdd) optionally followed by a letter used for rebuilds on the same day, which makes
    /// ordinal comparison correct: 20261116 &lt; 20261116b &lt; 20261117.
    /// </summary>
    public static class ReleaseTag
    {
        private static readonly Regex Shape =
            new(@"^(?<date>\d{8})(?<suffix>[a-zA-Z]*)", RegexOptions.Compiled);

        /// <summary>
        /// Key used for ordering. Returns null for tags that do not follow the scheme.
        /// </summary>
        public static string GetSortKey(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;

            Match match = Shape.Match(tag.Trim());
            if (!match.Success) return null;

            // Lower-case the suffix so 'B' and 'b' order identically.
            return match.Groups["date"].Value + match.Groups["suffix"].Value.ToLowerInvariant();
        }

        /// <summary>
        /// True when <paramref name="candidate"/> is a later release than <paramref name="running"/>.
        /// Unknown shapes fall back to the GitHub version comparison in the caller.
        /// </summary>
        public static bool IsNewer(string candidate, string running)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            if (string.IsNullOrWhiteSpace(running)) return true;

            string candidateKey = GetSortKey(candidate);
            string runningKey = GetSortKey(running);

            if (candidateKey == null || runningKey == null)
            {
                // Different naming scheme: treat as newer only when not the same tag.
                return !string.Equals(candidate, running, StringComparison.OrdinalIgnoreCase);
            }

            return string.CompareOrdinal(candidateKey, runningKey) > 0;
        }

        /// <summary>
        /// True when both tags point at the same build.
        /// </summary>
        public static bool AreSame(string first, string second) =>
            string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
