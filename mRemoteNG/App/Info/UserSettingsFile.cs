using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.App.Info
{
    /// <summary>
    /// Decides where the per-user settings (user.config) are read from and written to.
    ///
    /// .NET stores them under %LOCALAPPDATA%\&lt;company&gt;\&lt;app&gt;_Url_&lt;hash of the exe path&gt;\&lt;version&gt;\user.config,
    /// which means moving the program folder or changing the version starts from scratch. To make the
    /// settings survive that, this class keeps a copy in a stable location and restores it on start:
    ///
    /// 1. If user.config sits next to mRemoteNG.exe, that portable file is authoritative.
    /// 2. Otherwise the standard AppData copy (%APPDATA%\R&lt;product&gt;\user.config) is used and kept in sync.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class UserSettingsFile
    {
        private const string FileName = "user.config";

        /// <summary>
        /// user.config placed next to the executable. When it exists it always wins.
        /// </summary>
        public static string PortablePath { get; } =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);

        /// <summary>
        /// Stable per-user copy in AppData, independent of the program folder and version.
        /// </summary>
        public static string StandardPath { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         Application.ProductName, FileName);

        /// <summary>
        /// The file the .NET settings system actually uses for this executable and version.
        /// </summary>
        public static string RuntimePath { get; } =
            ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;

        public static bool IsPortable => File.Exists(PortablePath);

        /// <summary>
        /// The <c>System.Configuration.ConfigurationManager</c> assembly version of the runtime
        /// this process actually runs on (for example 9.0.0.0 or 10.0.0.0). The settings system
        /// records that version inside user.config and later insists on loading exactly that
        /// assembly, so a file written by a .NET 10 build cannot be read by a .NET 9 build.
        /// </summary>
        public static Version ConfigurationAssemblyVersion { get; } =
            typeof(ConfigurationManager).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

        /// <summary>
        /// Called once, before anything reads a setting: makes sure the file used by .NET contains
        /// the values the user expects (portable file first, then the standard AppData copy).
        /// </summary>
        public static void Restore()
        {
            try
            {
                if (IsPortable)
                {
                    if (!SamePath(PortablePath, RuntimePath))
                        Copy(PortablePath, RuntimePath);
                    NormalizeConfigurationVersion(RuntimePath);
                    NormalizeConfigurationVersion(PortablePath);
                    return;
                }

                if (!File.Exists(RuntimePath) && File.Exists(StandardPath))
                    Copy(StandardPath, RuntimePath);

                // The file used by the settings system must match the runtime that is running now.
                // Without this a settings file written on .NET 10 crashes a .NET 9 build at startup.
                NormalizeConfigurationVersion(RuntimePath);

                if (File.Exists(RuntimePath) && !File.Exists(StandardPath))
                    Copy(RuntimePath, StandardPath);

                NormalizeConfigurationVersion(StandardPath);
            }
            catch (Exception ex)
            {
                // Never block startup because of settings; the app just falls back to defaults.
                Debug.WriteLine($"Restoring user settings failed: {ex}");
            }
        }

        /// <summary>
        /// Called after the settings were saved: copies them to the portable file or to the
        /// standard AppData location.
        /// </summary>
        public static void Mirror()
        {
            try
            {
                if (!File.Exists(RuntimePath)) return;

                string target = IsPortable ? PortablePath : StandardPath;
                if (!SamePath(RuntimePath, target))
                    Copy(RuntimePath, target);

                // Also fix an old file written by a build on a different .NET major, so switching
                // between the framework-dependent and self-contained packages keeps working.
                NormalizeConfigurationVersion(RuntimePath);
                if (!SamePath(RuntimePath, target))
                    NormalizeConfigurationVersion(target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mirroring user settings failed: {ex}");
            }
        }

        /// <summary>
        /// Rewrites the assembly version recorded in the file's section declarations to the
        /// runtime that is running right now.
        ///
        /// user.config starts with lines such as
        /// &lt;section name="..." type="..., System.Configuration.ConfigurationManager, Version=10.0.0.0, ..." /&gt;
        /// Those versions are hard requirements, not hints: a .NET 9 build cannot load a handler that
        /// asks for the 10.0.0.0 assembly and dies with ConfigurationErrorsException at startup.
        /// Since the sections are the same in every version, patching the number is enough.
        /// </summary>
        public static void NormalizeConfigurationVersion(string path)
        {
            if (!File.Exists(path)) return;

            string expected = ConfigurationAssemblyVersion.ToString();
            string content;

            try
            {
                content = File.ReadAllText(path);
                // A truncated or otherwise broken file would crash the app on every start.
                // Parsing here turns that into "start with defaults" instead of a crash loop.
                System.Xml.Linq.XDocument.Parse(content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings file '{path}' is not readable ({ex.Message}); moving it aside.");
                TryMoveAside(path);
                return;
            }

            // Only touch declarations of the configuration manager itself.
            const string marker = "System.Configuration.ConfigurationManager, Version=";
            if (content.IndexOf(marker, StringComparison.Ordinal) < 0) return;

            string updated = ReplaceAssemblyVersion(content, marker, expected);
            if (string.Equals(content, updated, StringComparison.Ordinal)) return;

            try
            {
                File.WriteAllText(path, updated);
                Debug.WriteLine($"Updated the settings file for .NET {expected}: {path}");
            }
            catch (Exception ex)
            {
                // A read-only or locked file is not fatal: the settings system will complain,
                // which the caller sees as an ordinary load failure.
                Debug.WriteLine($"Could not update configuration versions in '{path}': {ex.Message}");
            }
        }

        private static void TryMoveAside(string path)
        {
            try
            {
                string backup = path + ".invalid";
                File.Move(path, backup, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not move '{path}' aside: {ex.Message}");
            }
        }

        private static string ReplaceAssemblyVersion(string content, string marker, string expected)
        {
            int index = 0;
            while ((index = content.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
            {
                int versionStart = index + marker.Length;
                int versionEnd = content.IndexOf(',', versionStart);
                if (versionEnd < 0) break;

                string current = content.Substring(versionStart, versionEnd - versionStart).Trim();
                if (current != expected)
                {
                    content = content.Substring(0, versionStart) + expected + content[versionEnd..];
                    index = versionStart + expected.Length;
                }
                else
                {
                    index = versionEnd;
                }
            }

            return content;
        }

        private static bool SamePath(string first, string second) =>
            string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

        private static void Copy(string source, string target)
        {
            string? directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.Copy(source, target, true);
        }
    }
}
