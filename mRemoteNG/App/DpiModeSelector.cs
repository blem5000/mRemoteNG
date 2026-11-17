using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows.Forms;
using System.Xml.Linq;

namespace mRemoteNG.App
{
    /// <summary>
    /// Chooses the WinForms DPI awareness mode at startup.
    ///
    /// The mode is configurable in Options > Appearance and stored in the normal settings. Because
    /// <see cref="Application.SetHighDpiMode"/> must be the very first WinForms-related call in
    /// <c>Main()</c>, the value is read straight from user.config XML - touching
    /// <c>ConfigurationManager</c> first makes WinForms ignore the DPI mode.
    ///
    /// The MREMOTENG_DPI_MODE environment variable is kept as an override for quick experiments
    /// without changing the saved option.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class DpiModeSelector
    {
        /// <summary>
        /// Name of the environment variable used to override the saved option.
        /// </summary>
        public const string EnvironmentVariable = "MREMOTENG_DPI_MODE";

        /// <summary>
        /// Name of the setting in OptionsAppearancePage.
        /// </summary>
        public const string SettingName = "DpiMode";

        /// <summary>
        /// Mode used when nothing is configured. Kept as upstream's value.
        /// </summary>
        public static HighDpiMode DefaultMode => HighDpiMode.SystemAware;

        /// <summary>
        /// Human-readable names offered in the Options combo box.
        /// </summary>
        public static IReadOnlyList<string> AllModeNames { get; } = new[]
        {
            nameof(HighDpiMode.DpiUnaware),
            nameof(HighDpiMode.SystemAware),
            nameof(HighDpiMode.PerMonitorV2),
            nameof(HighDpiMode.DpiUnawareGdiScaled)
        };

        public static string DefaultModeName => DefaultMode.ToString();

        public static bool IsKnownMode(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            Enum.TryParse(name.Trim(), ignoreCase: true, out HighDpiMode parsed) &&
            Enum.IsDefined(parsed);

        public static HighDpiMode GetMode(string overrideValue = null)
        {
            string requested = overrideValue ?? Environment.GetEnvironmentVariable(EnvironmentVariable);

            if (IsKnownMode(requested))
                return Parse(requested);

            return DefaultMode;
        }

        /// <summary>
        /// Reads the configured mode from the settings file without using ConfigurationManager.
        /// Checks the portable file first, then the standard AppData copy (which is kept in sync
        /// by UserSettingsFile.Mirror()).
        /// </summary>
        public static HighDpiMode GetConfiguredMode(IEnumerable<string> configPaths = null)
        {
            IEnumerable<string> paths = configPaths ?? CandidateConfigPaths();

            foreach (string path in paths)
            {
                string value = ReadSettingFromXml(path, SettingName);
                if (value != null && IsKnownMode(value))
                    return Parse(value);
            }

            return DefaultMode;
        }

        /// <summary>
        /// Paths that may contain the saved setting, most specific first.
        /// </summary>
        private static IEnumerable<string> CandidateConfigPaths()
        {
            if (File.Exists(Info.UserSettingsFile.PortablePath))
                yield return Info.UserSettingsFile.PortablePath;

            yield return Info.UserSettingsFile.StandardPath;
        }

        /// <summary>
        /// Extracts a userSettings value from user.config as plain text. Returns null on any
        /// problem: settings must never prevent the application from starting.
        /// </summary>
        public static string ReadSettingFromXml(string path, string settingName)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                XDocument document = XDocument.Load(path);
                XNamespace ns = "urn:schemas-microsoft-com:asm.v3";

                // The file may or may not use a namespace; support both.
                IEnumerable<XElement> settings = document.Descendants()
                    .Where(e => e.Name.LocalName == "setting");

                XElement match = settings.FirstOrDefault(
                    e => (string)e.Attribute("name") == settingName);

                return match?.Elements().FirstOrDefault(e => e.Name.LocalName == "value")?.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read {settingName} from '{path}': {ex.Message}");
                return null;
            }
        }

        private static HighDpiMode Parse(string value) =>
            (HighDpiMode)Enum.Parse(typeof(HighDpiMode), value.Trim(), ignoreCase: true);
    }
}
