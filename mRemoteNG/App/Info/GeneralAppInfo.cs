using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using static System.Environment;


namespace mRemoteNG.App.Info
{
    [SupportedOSPlatform("windows")]
    public static class GeneralAppInfo
    {
        public const string UrlHome = "https://mremoteng.org";
        public const string UrlDonate = "https://mremoteng.org/contribute";
        public const string UrlForum = "https://github.com/orgs/mRemoteNG/discussions";
        public const string UrlChat = "https://app.element.io/#/room/#mremoteng:matrix.org";
        public const string UrlCommunity = "https://www.reddit.com/r/mRemoteNG";
        public const string UrlBugs = "https://github.com/mRemoteNG/mRemoteNG/issues/new";
        public const string UrlDocumentation = "https://mremoteng.readthedocs.io/en/latest/";
        public static readonly string ApplicationVersion = Application.ProductVersion;
        public static readonly string ProductName = Application.ProductName;
        public static readonly string Copyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute), false))?.Copyright;
        public static readonly string HomePath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

        //public static string ReportingFilePath = "";
        private static readonly string puttyPath = HomePath + "\\PuTTYNG.exe";

        public static string UserAgent
        {
            get
            {
                List<string> details =
                [
                    "compatible",
                    OSVersion.Platform == PlatformID.Win32NT
                        ? $"Windows NT {OSVersion.Version.Major}.{OSVersion.Version.Minor}"
                        : OSVersion.VersionString
                ];
                if (Is64BitProcess)
                {
                    details.Add("WOW64");
                }

                details.Add(Thread.CurrentThread.CurrentUICulture.Name);
                details.Add($".NET CLR {Environment.Version}");
                string detailsString = string.Join("; ", [.. details]);

                return $"Mozilla/5.0 ({detailsString}) {ProductName}/{ApplicationVersion}";
            }
        }

        public static string PuttyPath => puttyPath;

        private static string? _commitHash;

        /// <summary>
        /// The git commit this build came from, when the build recorded it
        /// (AssemblyMetadata("Commit", ...) written by the build), or null.
        /// </summary>
        public static string? GetCommitHash()
        {
            if (_commitHash != null) return _commitHash.Length == 0 ? null : _commitHash;

            _commitHash = string.Empty;
            try
            {
                AssemblyMetadataAttribute? attribute = Assembly.GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => string.Equals(a.Key, "Commit", StringComparison.OrdinalIgnoreCase));

                if (attribute?.Value is { Length: >= 7 } value)
                    _commitHash = value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read the commit hash: {ex.Message}");
            }

            return _commitHash.Length == 0 ? null : _commitHash;
        }

        /// <summary>
        /// Build number embedded in the informational version ("1.78.2 (Nightly Build 3225) x64" -> 3225),
        /// or null when the string does not follow that shape.
        /// </summary>
        public static int? GetApplicationBuildNumber()
        {
            return GetBuildNumber(ApplicationVersion);
        }

        /// <summary>
        /// Parses the trailing build number out of an informational version string.
        /// </summary>
        public static int? GetBuildNumber(string informationalVersion)
        {
            if (string.IsNullOrWhiteSpace(informationalVersion))
                return null;

            Match match = BuildNumberPattern.Match(informationalVersion);
            return match.Success && int.TryParse(match.Groups[1].Value, out int build) ? build : null;
        }

        /// <summary>
        /// The version of the application, as a plain four-part version.
        ///
        /// The informational version looks like "1.78.2 (Nightly Build 3225) x64". The previous
        /// implementation bolted the *platform* onto the version ("1.78.2.x64"), which does not
        /// parse, so every caller silently received null and the update check compared nothing:
        /// it claimed "no update available" no matter what was published.
        /// </summary>
        public static Version GetApplicationVersion()
        {
            return GetVersion(ApplicationVersion, GetAssemblyVersion());
        }

        private static readonly Regex BuildNumberPattern = new(@"Build\s+(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex VersionPattern = new(@"\d+(?:\.\d+){1,3}", RegexOptions.Compiled);

        /// <summary>
        /// Reads the version out of an informational version string, falling back to the
        /// assembly version when nothing usable is found.
        /// </summary>
        public static Version GetVersion(string informationalVersion, Version fallback = null)
        {
            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                Match match = VersionPattern.Match(informationalVersion);
                if (match.Success && System.Version.TryParse(match.Value, out Version parsed))
                    return parsed;
            }

            return fallback ?? GetAssemblyVersion();
        }

        private static Version GetAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        }
    }
}