using System.Diagnostics;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.App.Update;
using mRemoteNG.Themes;
using mRemoteNG.Resources.Language;
using System.Reflection;
using mRemoteNG.Properties;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using mRemoteNG.UI.Window;

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class frmAbout : BaseWindow
    {
        public static frmAbout Instance { get; set; } = new frmAbout();

        public frmAbout()
        {
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.UIAboutBox_16x);
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
            ApplyLanguage();
            ApplyTheme();
        }

        private void ApplyLanguage()
        {
            lblLicense.Text = Language.ReleasedUnderGPL;
            base.Text = Language.MenuItem_About;
            TabText = Language.MenuItem_About;
            llChangelog.Text = Language.Changelog;
            llCredits.Text = Language.Credits;
            llLicense.Text = Language.License;
            lblCopyright.Text = GeneralAppInfo.Copyright;
            lblVersion.Text = $@"Version {GeneralAppInfo.ApplicationVersion}";
            lblBuild.Text = BuildDescription();
            AddPortableString();
        }

        /// <summary>
        /// Second line under the version: which fork build this is. The plain version is
        /// identical across releases, so without this there was no way to tell two downloads apart.
        /// </summary>
        private static string BuildDescription()
        {
            string[] parts =
            [
                $"Fork build {ForkUpdateChannel.BuildTag}",
                Runtime.IsSelfContainedBuild ? "self-contained" : "framework-dependent"
            ];

            string commit = GeneralAppInfo.GetCommitHash();
            if (!string.IsNullOrEmpty(commit))
                parts[0] += $" ({commit[..System.Math.Min(7, commit.Length)]})";

            return string.Join(" · ", parts);
        }

        [Conditional("PORTABLE")]
        private void AddPortableString() => lblTitle.Text += $@" {Language.PortableEdition}";

        private void ApplyTheme()
        {
            if (!ThemeManager.getInstance().ThemingActive) return;
            ThemeManager.getInstance().ApplyThemeToTitleBar(this);
            if (!ThemeManager.getInstance().ActiveAndExtended) return;
            pnlBottom.BackColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            pnlBottom.ForeColor = ThemeManager.getInstance().ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            
            // Don't cancel close when shown in DockPanel
            // This allows the tab to close properly without showing connection close dialog
        }

        private void llLicense_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenForkFile("COPYING.txt");
        }

        private void llChangelog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenForkFile("CHANGELOG.md");
        }

        private void llCredits_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenForkFile("CREDITS.md");
        }

        /// <summary>
        /// Opens a file from this fork's repository on GitHub.
        ///
        /// The old links pointed at mRemoteNG/mRemoteNG with a tag built from the assembly
        /// version and the update channel ("v1.78.2-Nightly"), which does not exist — all three
        /// links returned 404. They now point at the fork the user actually downloaded.
        /// </summary>
        private static void OpenForkFile(string fileName)
        {
            OpenUrl($"{ForkUpdateChannel.RepositoryUrl}/blob/{ForkUpdateChannel.BranchName}/{fileName}");
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}