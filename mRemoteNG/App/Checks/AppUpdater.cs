using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using mRemoteNG.App.Info;
using mRemoteNG.Properties;
using mRemoteNG.Security;
using mRemoteNG.Security.SymmetricEncryption;
using System.Runtime.Versioning;

// ReSharper disable ArrangeAccessorOwnerBody

namespace mRemoteNG.App.Update
{
    /// <summary>
    /// Checks this fork's GitHub releases for updates and downloads the matching zip asset.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AppUpdater
    {
        private const int _bufferLength = 8192;

        private WebProxy _webProxy;
        private HttpClient _httpClient;
        private CancellationTokenSource _changeLogCancelToken;
        private CancellationTokenSource _getUpdateInfoCancelToken;

        #region Public Properties

        public UpdateInfo CurrentUpdateInfo { get; private set; }

        public bool IsGetUpdateInfoRunning
        {
            get
            {
                return _getUpdateInfoCancelToken != null;
            }
        }

        private bool IsGetChangeLogRunning
        {
            get
            {
                return _changeLogCancelToken != null;
            }
        }

        #endregion

        #region Public Methods

        public AppUpdater()
        {
            SetDefaultProxySettings();
        }

        private void SetDefaultProxySettings()
        {
            bool shouldWeUseProxy = Properties.OptionsUpdatesPage.Default.UpdateUseProxy;
            string proxyAddress = Properties.OptionsUpdatesPage.Default.UpdateProxyAddress;
            int port = Properties.OptionsUpdatesPage.Default.UpdateProxyPort;
            bool useAuthentication = Properties.OptionsUpdatesPage.Default.UpdateProxyUseAuthentication;
            string username = Properties.OptionsUpdatesPage.Default.UpdateProxyAuthUser;
            // Use the same DPAPI-aware helper as every other consumer of this setting. Decrypting
            // with the legacy default key throws on a dpapi:-prefixed value and could fail at
            // startup (AppUpdater is constructed from a static initializer).
            string password;
            try
            {
                password = OptionPasswords.Unprotect(Properties.OptionsUpdatesPage.Default.UpdateProxyAuthPass);
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector?.AddExceptionMessage("Could not read the saved proxy password.", ex);
                password = string.Empty;
            }

            SetProxySettings(shouldWeUseProxy, proxyAddress, port, useAuthentication, username, password);
        }

        public void SetProxySettings(bool useProxy, string address, int port, bool useAuthentication, string username, string password)
        {
            if (useProxy && !string.IsNullOrEmpty(address))
            {
                _webProxy = port != 0 ? new WebProxy(address, port) : new WebProxy(address);
                _webProxy.Credentials = useAuthentication ? new NetworkCredential(username, password) : null;
            }
            else
            {
                _webProxy = null;
            }

            UpdateHttpClient();
        }

        /// <summary>
        /// True when the latest release in the fork repository is not the build that is running.
        ///
        /// Two independent signals are used, because either one alone gives wrong answers here:
        /// - the tag: every release keeps the version (1.78.2) and only the tag changes, so a
        ///   newer tag means a newer build even when the version is identical;
        /// - the version: catches a release published with a genuinely higher version number.
        /// </summary>
        public bool IsUpdateAvailable()
        {
            if (CurrentUpdateInfo == null || !CurrentUpdateInfo.IsValid)
            {
                return false;
            }

            if (ReleaseTag.AreSame(CurrentUpdateInfo.BuildTag, ForkUpdateChannel.BuildTag))
                return false;

            if (ReleaseTag.IsNewer(CurrentUpdateInfo.BuildTag, ForkUpdateChannel.BuildTag))
                return true;

            Version runningVersion = GeneralAppInfo.GetApplicationVersion();
            if (runningVersion != null && CurrentUpdateInfo.Version > runningVersion)
                return true;

            // A tag that cannot be ordered (for example an older naming scheme) still counts as
            // an update when it is simply a different build.
            return ReleaseTag.GetSortKey(CurrentUpdateInfo.BuildTag) == null
                   || ReleaseTag.GetSortKey(ForkUpdateChannel.BuildTag) == null;
        }

        public async Task DownloadUpdateAsync(IProgress<int> progress)
        {
            if (IsGetUpdateInfoRunning)
            {
                _getUpdateInfoCancelToken.Cancel();
                _getUpdateInfoCancelToken.Dispose();
                _getUpdateInfoCancelToken = null;

                throw new InvalidOperationException("A previous call to DownloadUpdateAsync() is still in progress.");
            }

            if (CurrentUpdateInfo == null || !CurrentUpdateInfo.IsValid)
            {
                throw new InvalidOperationException("CurrentUpdateInfo is not valid. GetUpdateInfoAsync() must be called before calling DownloadUpdateAsync().");
            }

            string updateDirectory = Path.Combine(Path.GetTempPath(), "mRemoteNG", "updates");
            Directory.CreateDirectory(updateDirectory);
            // FileName comes from the GitHub API (asset name). Never trust it as a path: strip
            // any directory component so a malicious/compromised response cannot write outside
            // the update directory via "../" or an absolute path.
            string safeFileName = Path.GetFileName(CurrentUpdateInfo.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName) ||
                safeFileName.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            {
                throw new InvalidOperationException("Invalid update file name received from the update server.");
            }
            string updateFilePath = Path.Combine(updateDirectory, safeFileName);

            try
            {
                _getUpdateInfoCancelToken = new CancellationTokenSource();
                using HttpResponseMessage response = await _httpClient.GetAsync(CurrentUpdateInfo.DownloadAddress, HttpCompletionOption.ResponseHeadersRead, _getUpdateInfoCancelToken.Token);
                byte[] buffer = new byte[_bufferLength];
                long totalBytes = response.Content.Headers.ContentLength ?? 0;
                long readBytes = 0L;

                await using (Stream httpStream = await response.Content.ReadAsStreamAsync(_getUpdateInfoCancelToken.Token))
                {
                    await using FileStream fileStream = new(updateFilePath, FileMode.Create,
                        FileAccess.Write, FileShare.None, _bufferLength, true);

                    while (true)
                    {
                        int bytesRead =
                            await httpStream.ReadAsync(buffer.AsMemory(0, _bufferLength), _getUpdateInfoCancelToken.Token);
                        if (bytesRead == 0)
                        {
                            progress.Report(100);
                            break;
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _getUpdateInfoCancelToken.Token);

                        readBytes += bytesRead;

                        if (totalBytes > 0)
                            progress.Report((int)(readBytes * 100 / totalBytes));
                    }
                }

                if (!string.IsNullOrEmpty(CurrentUpdateInfo.Checksum))
                    await VerifyChecksumAsync(updateFilePath, CurrentUpdateInfo.Checksum, _getUpdateInfoCancelToken.Token);

                CurrentUpdateInfo.UpdateFilePath = updateFilePath;
            }
            finally
            {
                _getUpdateInfoCancelToken?.Dispose();
                _getUpdateInfoCancelToken = null;
            }
        }

        #endregion

        #region Private Methods

        private static async Task VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken cancellationToken)
        {
            using SHA256 checksum = SHA256.Create();
            await using FileStream stream = File.OpenRead(filePath);
            byte[] hash = await checksum.ComputeHashAsync(stream, cancellationToken);
            string hashString = Convert.ToHexString(hash);

            if (!hashString.Equals(expectedChecksum, StringComparison.OrdinalIgnoreCase))
                throw new Exception("SHA256 hashes didn't match. The downloaded update was discarded.");
        }

        private void UpdateHttpClient()
        {
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }

            HttpClientHandler httpClientHandler = new();
            if (_webProxy != null)
            {
                httpClientHandler.UseProxy = true;
                httpClientHandler.Proxy = _webProxy;
            }

            _httpClient = new HttpClient(httpClientHandler);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(GeneralAppInfo.UserAgent);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        public async Task GetUpdateInfoAsync()
        {
            if (IsGetUpdateInfoRunning)
            {
                _getUpdateInfoCancelToken.Cancel();
                _getUpdateInfoCancelToken.Dispose();
                _getUpdateInfoCancelToken = null;
            }

            try
            {
                _getUpdateInfoCancelToken = new CancellationTokenSource();
                string releasesJson = await _httpClient.GetStringAsync(ForkUpdateChannel.ReleasesApiUri, _getUpdateInfoCancelToken.Token);
                CurrentUpdateInfo = GitHubReleaseParser.ParseLatestRelease(releasesJson, IsPreviewChannel(),
                                                                          ForkUpdateChannel.AssetSuffix);
                Properties.OptionsUpdatesPage.Default.CheckForUpdatesLastCheck = DateTime.UtcNow;

                if (!Properties.OptionsUpdatesPage.Default.UpdatePending)
                {
                    Properties.OptionsUpdatesPage.Default.UpdatePending = IsUpdateAvailable();
                }
            }
            finally
            {
                _getUpdateInfoCancelToken?.Dispose();
                _getUpdateInfoCancelToken = null;
            }
        }

        public Task<string> GetChangeLogAsync()
        {
            if (IsGetChangeLogRunning)
            {
                _changeLogCancelToken.Cancel();
                _changeLogCancelToken.Dispose();
                _changeLogCancelToken = null;
            }

            // The release body already came with the release listing, so no second request is needed.
            _changeLogCancelToken = null;
            return Task.FromResult(CurrentUpdateInfo?.ChangeLog ?? string.Empty);
        }

        private static bool IsPreviewChannel()
        {
            string channel = Properties.OptionsUpdatesPage.Default.UpdateChannel ?? string.Empty;
            return channel.Equals("dev", StringComparison.OrdinalIgnoreCase)
                   || channel.Equals("nightly", StringComparison.OrdinalIgnoreCase)
                   || channel.Equals("preview", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
