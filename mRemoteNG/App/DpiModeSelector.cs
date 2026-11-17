using System;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace mRemoteNG.App
{
    /// <summary>
    /// Chooses the WinForms DPI awareness mode at startup.
    ///
    /// Upstream switched to PerMonitorV2 in 2026 to fix small fonts at 125% scaling, but on some
    /// setups that mode makes the whole interface (menus, tree rows, icons) noticeably larger than
    /// the old .NET 9 build, which had no explicit DPI mode at all.
    ///
    /// The mode can be overridden without rebuilding:
    ///   MREMOTENG_DPI_MODE=SystemAware    - old behaviour (scale once at startup)
    ///   MREMOTENG_DPI_MODE=DpiUnaware     - let Windows bitmap-scale the app
    ///   MREMOTENG_DPI_MODE=PerMonitorV2   - per-monitor scaling (larger interface)
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class DpiModeSelector
    {
        /// <summary>
        /// Name of the environment variable used to override the DPI mode.
        /// </summary>
        public const string EnvironmentVariable = "MREMOTENG_DPI_MODE";

        /// <summary>
        /// Returns the mode to pass to <see cref="Application.SetHighDpiMode"/>.
        /// Unknown values are ignored and the default is returned.
        /// </summary>
        public static HighDpiMode GetMode(string overrideValue = null)
        {
            string requested = overrideValue ?? Environment.GetEnvironmentVariable(EnvironmentVariable);

            if (string.IsNullOrWhiteSpace(requested))
                return DefaultMode;

            if (Enum.TryParse(requested.Trim(), ignoreCase: true, out HighDpiMode parsed) &&
                Enum.IsDefined(parsed))
                return parsed;

            return DefaultMode;
        }

        /// <summary>
        /// Mode used when there is no override.
        ///
        /// SystemAware restores the look of the .NET 9 build (smaller menus, tree rows and icons).
        /// Upstream switched to PerMonitorV2 in 2026 to fix small fonts at 125% scaling, but that
        /// mode makes the whole interface noticeably larger on common setups. Users can still opt
        /// into it with MREMOTENG_DPI_MODE=PerMonitorV2.
        /// </summary>
        public static HighDpiMode DefaultMode => HighDpiMode.SystemAware;
    }
}
