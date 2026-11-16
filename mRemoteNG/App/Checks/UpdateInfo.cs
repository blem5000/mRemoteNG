using System;

// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace mRemoteNG.App.Update
{
    public class UpdateInfo
    {
        public bool IsValid { get; private set; }
        public Version Version { get; private set; }
        public Uri DownloadAddress { get; private set; }
        public string UpdateFilePath { get; set; }
        public Uri ChangeLogAddress { get; private set; }
        public Uri ImageAddress { get; private set; }
        public Uri ImageLinkAddress { get; private set; }
#if !PORTABLE
        public string CertificateThumbprint { get; private set; }
#endif
        // ReSharper disable once MemberCanBePrivate.Global
        public string FileName { get; set; }
        public string Checksum { get; private set; }

        /// <summary>
        /// Release tag the update was published under (for example 20261116-v1.78.2-NB).
        /// </summary>
        public string BuildTag { get; private set; }

        /// <summary>
        /// Release notes text, shown in the update window.
        /// </summary>
        public string ChangeLog { get; private set; }

        /// <summary>
        /// Builds update information from a GitHub release.
        /// </summary>
        public static UpdateInfo FromGitHubRelease(string buildTag, Version version, string fileName,
                                                   Uri downloadAddress, string changeLog, string checksum)
        {
            UpdateInfo newInfo = new()
            {
                BuildTag = buildTag,
                Version = version,
                FileName = fileName,
                DownloadAddress = downloadAddress,
                ChangeLogAddress = downloadAddress,
                ChangeLog = changeLog ?? string.Empty,
                Checksum = checksum ?? string.Empty,
                IsValid = version != null && !string.IsNullOrEmpty(fileName) && downloadAddress != null
            };

            return newInfo;
        }

        public static UpdateInfo FromString(string input)
        {
            UpdateInfo newInfo = new();
            if (string.IsNullOrEmpty(input))
            {
                newInfo.IsValid = false;
            }
            else
            {
                UpdateFile updateFile = new(input);
                newInfo.Version = updateFile.GetVersion();
                newInfo.DownloadAddress = updateFile.GetUri("dURL");
                newInfo.ChangeLogAddress = updateFile.GetUri("clURL");
#if false
                newInfo.ImageAddress = updateFile.GetUri("imgURL");
                newInfo.ImageLinkAddress = updateFile.GetUri("imgURLLink");
#endif
#if !PORTABLE
                newInfo.CertificateThumbprint = updateFile.GetThumbprint();
#endif
                newInfo.FileName = updateFile.GetFileName();
                newInfo.Checksum = updateFile.GetChecksum();
                newInfo.IsValid = newInfo.CheckIfValid();
            }

            return newInfo;
        }

        public bool CheckIfValid()
        {
            if (string.IsNullOrEmpty(Version.ToString()))
                return false;
            if (string.IsNullOrEmpty(DownloadAddress.AbsoluteUri))
                return false;
            if (string.IsNullOrEmpty(ChangeLogAddress.AbsoluteUri))
                return false;
#if false
            if (string.IsNullOrEmpty(ImageAddress.AbsoluteUri))
                return false;
            if (string.IsNullOrEmpty(ImageLinkAddress.AbsoluteUri))
                return false;
#endif
#if !PORTABLE
            if (string.IsNullOrEmpty(CertificateThumbprint))
                return false;
#endif
            if (string.IsNullOrEmpty(FileName))
                return false;
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (string.IsNullOrEmpty(Checksum))
                return false;

            return true;
        }
    }
}