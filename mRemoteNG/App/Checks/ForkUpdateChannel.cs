using System;
using System.IO;
using System.Runtime.Versioning;

namespace mRemoteNG.App.Update
{
    /// <summary>
    /// Describes which GitHub repository this build updates from and which asset fits it.
    ///
    /// The fork publishes one release per version with two assets:
    /// mRemoteNG-1.78.2-fix-YYYYMMDD-x64.zip (framework-dependent) and
    /// mRemoteNG-1.78.2-fix-YYYYMMDD-x64-selfcontained.zip (bundles the .NET runtime).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ForkUpdateChannel
    {
        public const string RepositoryOwner = "blem5000";
        public const string RepositoryName = "mRemoteNG";

        /// <summary>
        /// Branch the releases are built from, used for "about" links.
        /// </summary>
        public const string BranchName = "main";

        public static string RepositoryUrl =>
            $"https://github.com/{RepositoryOwner}/{RepositoryName}";

        /// <summary>
        /// Release tag this build was produced from. Bump it together with every release so the
        /// updater can tell "same version, newer build" apart from "newer version".
        /// </summary>
        public const string BuildTag = "20261123b-v1.78.2-NB";

        public const string FrameworkDependentAssetSuffix = "-x64.zip";
        public const string SelfContainedAssetSuffix = "-x64-selfcontained.zip";

        /// <summary>
        /// True when the app runs on the runtime bundled next to mRemoteNG.exe.
        /// </summary>
        public static bool IsSelfContainedBuild =>
            File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "coreclr.dll"));

        /// <summary>
        /// Asset suffix matching the way this build was published.
        /// </summary>
        public static string AssetSuffix =>
            IsSelfContainedBuild ? SelfContainedAssetSuffix : FrameworkDependentAssetSuffix;

        public static Uri ReleasesApiUri =>
            new($"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=15");
    }
}


