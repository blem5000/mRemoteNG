using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace mRemoteNG.DotNet.Update
{
    [SupportedOSPlatform("windows")]
    public class DotNetRuntimeCheck
    {
        /// <summary>
        /// Channel shown to the user when the runtime is missing.
        /// </summary>
        public const string RequiredDotnetVersion = "9.0";

        /// <summary>
        /// Lowest major version this build can run on. The framework-dependent package is
        /// published with RollForward=LatestMajor, so any newer major (10, 11, ...) is fine too.
        /// </summary>
        public const int RequiredMajorVersion = 9;

        private const string ReleaseFeedUrl = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json";
        private const string DesktopFrameworkName = "Microsoft.WindowsDesktop.App";

        #region Installed Version Check

        /// <summary>
        /// Returns the newest installed desktop runtime that this build can run on, or null
        /// when there is none.
        ///
        /// This looks at the actual shared framework folders (shared\Microsoft.WindowsDesktop.App\9.0.x).
        /// The registry value ...\InstalledVersions\x64\sharedhost\Version is *not* used: it only
        /// says that some .NET host is installed (for example 8.0), which is not enough for this
        /// application and produced a wrong "runtime is installed" answer.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static string? GetLatestDotNetRuntimeVersion()
        {
            return FindInstalledDesktopRuntime(GetDotNetRoots(), RequiredMajorVersion);
        }

        /// <summary>
        /// Finds the highest installed desktop runtime version with at least
        /// <paramref name="requiredMajor"/> in any of the given dotnet roots.
        /// Public and parameterised so it can be tested without touching the real machine.
        /// </summary>
        public static string? FindInstalledDesktopRuntime(IEnumerable<string> dotnetRoots, int requiredMajor)
        {
            if (dotnetRoots == null || requiredMajor <= 0)
                return null;

            Version? best = null;
            string? bestVersion = null;

            foreach (string root in dotnetRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                string sharedPath = Path.Combine(root, "shared", DesktopFrameworkName);
                string[] versionDirectories;
                try
                {
                    if (!Directory.Exists(sharedPath))
                        continue;

                    versionDirectories = Directory.GetDirectories(sharedPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not inspect '{sharedPath}': {ex.Message}");
                    continue;
                }

                foreach (string versionDirectory in versionDirectories)
                {
                    string name = Path.GetFileName(versionDirectory);
                    if (!Version.TryParse(name, out Version? version))
                        continue;

                    if (version.Major < requiredMajor)
                        continue;

                    if (best != null && version <= best)
                        continue;

                    best = version;
                    bestVersion = name;
                }
            }

            return bestVersion;
        }

        /// <summary>
        /// All places where a .NET installation can live, most likely first.
        /// </summary>
        public static IEnumerable<string> GetDotNetRoots()
        {
            List<string> roots = new();

            void Add(string? path)
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                if (roots.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
                roots.Add(path);
            }

            Add(Environment.GetEnvironmentVariable("DOTNET_ROOT_X64"));
            Add(Environment.GetEnvironmentVariable("DOTNET_ROOT"));
            Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"));
            Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet"));

            Add(ReadRegistryInstallLocation(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64"));

            return roots;
        }

        private static string? ReadRegistryInstallLocation(string subKey)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(subKey);
                return key?.GetValue("InstallLocation") as string;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not read '{subKey}': {ex.Message}");
                return null;
            }
        }

        #endregion Installed Version Check

        #region Latest Online Version Check

        /// <summary>
        /// Asks Microsoft for the newest release of the required channel so the download
        /// dialog can point at a real installer. Returns an empty URL when offline.
        /// </summary>
        public static async Task<(string latestRuntimeVersion, string downloadUrl)> GetLatestAvailableDotNetVersionAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "DotNetRuntimeChecker");
                httpClient.Timeout = TimeSpan.FromSeconds(15);

                string jsonContent = await httpClient.GetStringAsync(ReleaseFeedUrl);
                JObject releasesIndex = JObject.Parse(jsonContent);

                // Find the entry for .NET matching RequiredDotnetVersion
                JToken? dotnetEntry = releasesIndex["releases-index"]?.FirstOrDefault(entry => entry["channel-version"]?.ToString() == RequiredDotnetVersion);

                if (dotnetEntry != null && dotnetEntry["latest-runtime"] != null)
                {
                    string? latestRuntimeVersion = dotnetEntry["latest-runtime"]?.ToString();
                    string arch;
                    switch (RuntimeInformation.OSArchitecture)
                    {
                        case Architecture.Arm64:
                            arch = "arm64";
                            break;
                        case Architecture.X86:
                            arch = "x86";
                            break;
                        case Architecture.X64:
                            arch = "x64";
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}");
                    }
                    if (!string.IsNullOrEmpty(latestRuntimeVersion))
                    {
                        // Construct the download URL using the latest version
                        string downloadUrl = $"https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-{latestRuntimeVersion}-windows-{arch}-installer";
                        return (latestRuntimeVersion, downloadUrl);
                    }
                }

                return ("Unknown", "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching latest version: {ex.Message}");
                return ("Unknown", "");
            }
        }

        #endregion Latest Online Version Check
    }
}
