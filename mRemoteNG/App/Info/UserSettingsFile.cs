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
                    return;
                }

                if (File.Exists(RuntimePath))
                {
                    // Refresh the standard copy so the next start (or a moved folder) finds it.
                    if (!File.Exists(StandardPath))
                        Copy(RuntimePath, StandardPath);
                    return;
                }

                if (File.Exists(StandardPath))
                    Copy(StandardPath, RuntimePath);
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
                if (SamePath(RuntimePath, target)) return;

                Copy(RuntimePath, target);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Mirroring user settings failed: {ex}");
            }
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
