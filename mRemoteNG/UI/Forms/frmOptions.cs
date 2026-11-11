#region  Usings
using mRemoteNG.UI.Forms.OptionsPages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using mRemoteNG.Themes;
using System.Configuration;
using System.Drawing;
using mRemoteNG.Properties;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;
#endregion

namespace mRemoteNG.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class FrmOptions : Form
    {
        private readonly List<OptionsPage> _optionPages = [];
        private string _pageName;
        private readonly DisplayProperties _display = new();
        private readonly List<string> _optionPageObjectNames;
        private bool _isLoading = true;
        private bool _isInitialized = false; // Add this field to the FrmOptions class

        /// <summary>
        /// Raised when the user confirms (OK) or aborts (Cancel) the dialog.
        /// The form does not close itself: it is embedded in <see cref="Window.OptionsWindow"/>,
        /// which decides how to close the hosting window.
        /// </summary>
        public event EventHandler? CloseRequested;

        public FrmOptions() : this(Language.StartupExit)
        {
        }

        private FrmOptions(string pageName)
        {
            Cursor.Current = Cursors.WaitCursor;
            Application.DoEvents();
            InitializeComponent();
            Icon = Resources.ImageConverter.GetImageAsIcon(Properties.Resources.Settings_16x);
            _pageName = pageName;
            Cursor.Current = Cursors.Default;

            _optionPageObjectNames =
            [
                nameof(StartupExitPage),
                nameof(AppearancePage),
                nameof(ConnectionsPage),
                nameof(TabsPanelsPage),
                nameof(NotificationsPage),
                nameof(CredentialsPage),
                nameof(SqlServerPage),
                nameof(UpdatesPage),
                nameof(ThemePage),
                nameof(SecurityPage),
                nameof(AdvancedPage),
                nameof(BackupPage)
            ];

            InitOptionsPagesToListView();
        }

        private void FrmOptions_Load(object sender, EventArgs e)
        {
            // Only initialize once to prevent multiple event subscriptions and page reloading
            if (_isInitialized)
            {
                this.Visible = true;
                return;
            }

            this.Visible = true;
            FontOverrider.FontOverride(this);
            SetActivatedPage();
            //ApplyLanguage();
            // Handle the main page here and the individual pages in
            // AddOptionsPagesToListView()  -- one less foreach loop....
            Text = Language.OptionsPageTitle;
            btnOK.Text = Language._Ok;
            btnCancel.Text = Language._Cancel;
            btnApply.Text = Language.Apply;
            ApplyTheme();
            ThemeManager.getInstance().ThemeChanged += ApplyTheme;
            lstOptionPages.SelectedIndexChanged += LstOptionPages_SelectedIndexChanged;
            lstOptionPages.SelectedIndex = 0;
            
            // Handle visibility changes to ensure panel is populated when form is shown
            this.VisibleChanged += FrmOptions_VisibleChanged;

            // Mark as initialized
            _isInitialized = true;
        }

        private void FrmOptions_VisibleChanged(object sender, EventArgs e)
        {
            // When the form becomes visible, ensure the panel is populated with the selected page
            if (this.Visible && pnlMain.Controls.Count == 0)
            {
                OptionsPage page = (OptionsPage)lstOptionPages.SelectedObject;
                if (page != null)
                    pnlMain.Controls.Add(page);
            }
        }

        private void ApplyTheme()
        {
            ThemeManager themeManager = ThemeManager.getInstance();
            if (!themeManager.ActiveAndExtended) return;

            Color background = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Background");
            Color foreground = themeManager.ActiveTheme.ExtendedPalette.getColor("Dialog_Foreground");

            BackColor = background;
            ForeColor = foreground;

            // The button bar, the page host and the splitters used to keep the system (light) colors,
            // which left a bright strip at the bottom of the dialog when a dark theme was active.
            pnlBottom.BackColor = background;
            pnlBottom.ForeColor = foreground;
            pnlMain.BackColor = background;
            pnlMain.ForeColor = foreground;

            // Splitter always paints its own system-colored border when BorderStyle is FixedSingle,
            // so paint the 1px separator with a theme color instead.
            Color separator = themeManager.ActiveTheme.ExtendedPalette.getColor("List_Item_Border");
            splitter1.BorderStyle = BorderStyle.None;
            splitter1.BackColor = separator;
            splitter2.BackColor = separator;

            Invalidate(true);
        }

#if false
        private void ApplyLanguage()
        {
            Text = Language.OptionsPageTitle;
            foreach (var optionPage in _pages.Values)
            {
                optionPage.ApplyLanguage();
            }
        }
#endif

        private void InitOptionsPagesToListView()
        {
            lstOptionPages.RowHeight = _display.ScaleHeight(lstOptionPages.RowHeight);
            lstOptionPages.AllColumns.First().ImageGetter = ImageGetter;

            // All pages are created synchronously. Previously only the first page was
            // created here and the rest were deferred to Application.Idle, which never
            // runs in tests and made the window appear empty on the first open.
            lstOptionPages.BeginUpdate();
            try
            {
                foreach (string pageName in _optionPageObjectNames)
                    InitOptionsPage(pageName);
            }
            finally
            {
                lstOptionPages.EndUpdate();
            }

            // All pages loaded, now start tracking changes
            _isLoading = false;
        }

        private void InitOptionsPage(string pageName)
        {
            OptionsPage page = null;

            switch (pageName)
            {
                case "StartupExitPage":
                    {
                        if (Properties.OptionsStartupExitPage.Default.cbStartupExitPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new StartupExitPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "AppearancePage":
                    {
                        if (Properties.OptionsAppearancePage.Default.cbAppearancePageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new AppearancePage { Dock = DockStyle.Fill };
                        break;
                    }
                case "ConnectionsPage":
                    {
                        if (Properties.OptionsConnectionsPage.Default.cbConnectionsPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new ConnectionsPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "TabsPanelsPage":
                    {
                        if (Properties.OptionsTabsPanelsPage.Default.cbTabsPanelsPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new TabsPanelsPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "NotificationsPage":
                    {
                        if (Properties.OptionsNotificationsPage.Default.cbNotificationsPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new NotificationsPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "CredentialsPage":
                    {
                        if (Properties.OptionsCredentialsPage.Default.cbCredentialsPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new CredentialsPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "SqlServerPage":
                    {
                        if (Properties.OptionsDBsPage.Default.cbDBsPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new SqlServerPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "UpdatesPage":
                    {
                        if (Properties.OptionsUpdatesPage.Default.cbUpdatesPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new UpdatesPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "ThemePage":
                    {
                        if (Properties.OptionsThemePage.Default.cbThemePageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new ThemePage { Dock = DockStyle.Fill };
                        break;
                    }
                case "SecurityPage":
                    {
                        if (Properties.OptionsSecurityPage.Default.cbSecurityPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new SecurityPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "AdvancedPage":
                    {
                        if (Properties.OptionsAdvancedPage.Default.cbAdvancedPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new AdvancedPage { Dock = DockStyle.Fill };
                        break;
                    }
                case "BackupPage":
                    {
                        if (Properties.OptionsBackupPage.Default.cbBacupPageInOptionMenu ||
                            Properties.OptionsRbac.Default.ActiveRole == "AdminRole")
                            page = new BackupPage { Dock = DockStyle.Fill };
                        break;
                    }
            }

            if (page == null) return;
            page.ApplyLanguage();
            page.LoadRegistrySettings();
            page.LoadSettings();
            _optionPages.Add(page);
            lstOptionPages.AddObject(page);
            
            // Track changes in all controls on the page
            TrackChangesInControls(page);
        }

        private object ImageGetter(object rowobject)
        {
            OptionsPage page = rowobject as OptionsPage;
            return page?.PageIcon == null ? _display.ScaleImage(Properties.Resources.F1Help_16x) : _display.ScaleImage(page.PageIcon);
        }

        public void SetActivatedPage(string pageName = default)
        {
            _pageName = pageName ?? Language.StartupExit;

            bool isSet = false;
            for (int i = 0; i < lstOptionPages.Items.Count; i++)
            {
                if (!lstOptionPages.Items[i].Text.Equals(_pageName)) continue;
                lstOptionPages.Items[i].Selected = true;
                isSet = true;
                break;
            }

            if (!isSet)
                lstOptionPages.Items[0].Selected = true;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SaveOptions();
            // Clear change flags after saving
            ClearChangeFlags();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BtnApply_Click(object sender, EventArgs e)
        {
            SaveOptions();
            // Clear change flags after applying
            ClearChangeFlags();
        }

        private void SaveOptions()
        {
            foreach (OptionsPage page in _optionPages)
            {
                Debug.WriteLine(page.PageName);
                page.SaveSettings();
            }

            Debug.WriteLine((ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None)).FilePath);
            Settings.Default.Save();
        }

        private void LstOptionPages_SelectedIndexChanged(object sender, EventArgs e)
        {
            pnlMain.Controls.Clear();

            OptionsPage page = (OptionsPage)lstOptionPages.SelectedObject;
            if (page != null)
                pnlMain.Controls.Add(page);
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // Cancel discards pending edits instead of silently keeping them.
            ReloadAllSettings();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Returns true if any options page has been modified.
        /// </summary>
        internal bool HasUnsavedChanges() => _optionPages.Any(page => page.HasChanges);

        /// <summary>
        /// Reloads all pages from the stored settings, discarding any pending control edits.
        /// </summary>
        internal void ReloadAllSettings()
        {
            // Suppress change tracking while the control values are restored programmatically
            bool wasLoading = _isLoading;
            _isLoading = true;
            try
            {
                foreach (OptionsPage page in _optionPages)
                    page.LoadSettings();
            }
            finally
            {
                _isLoading = wasLoading;
            }

            ClearChangeFlags();
        }

        private void FrmOptions_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Note: this dialog is created once per session and embedded in OptionsWindow, and
            // every close is cancelled here (the form is only hidden), so it must stay subscribed
            // to ThemeChanged - a single instance is not a leak.
            // Check if any page has unsaved changes
            bool hasChanges = _optionPages.Any(page => page.HasChanges);
            
            if (hasChanges)
            {
                DialogResult result = MessageBox.Show(
                    Language.SaveOptionsBeforeClosing,
                    Language.Options,
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);
                
                switch (result)
                {
                    case DialogResult.Yes:
                        SaveOptions();
                        ClearChangeFlags();
                        e.Cancel = true;
                        this.Visible = false;
                        break;
                    case DialogResult.No:
                        // Discard changes
                        ClearChangeFlags();
                        e.Cancel = true;
                        this.Visible = false;
                        break;
                    case DialogResult.Cancel:
                        // Cancel closing - keep the dialog open
                        e.Cancel = true;
                        break;
                }
            }
            else
            {
                e.Cancel = true;
                this.Visible = false;
            }
        }

        private void TrackChangesInControls(Control control)
        {
            foreach (Control childControl in control.Controls)
            {
                // Track changes for common input controls
                if (childControl is TextBox textBox)
                {
                    textBox.TextChanged += (s, e) => MarkPageAsChanged(control);
                }
                else if (childControl is CheckBox checkBox)
                {
                    checkBox.CheckedChanged += (s, e) => MarkPageAsChanged(control);
                }
                else if (childControl is RadioButton radioButton)
                {
                    radioButton.CheckedChanged += (s, e) => MarkPageAsChanged(control);
                }
                else if (childControl is ComboBox comboBox)
                {
                    comboBox.SelectedIndexChanged += (s, e) => MarkPageAsChanged(control);
                }
                else if (childControl is NumericUpDown numericUpDown)
                {
                    numericUpDown.ValueChanged += (s, e) => MarkPageAsChanged(control);
                }
                else if (childControl is ListBox listBox)
                {
                    listBox.SelectedIndexChanged += (s, e) => MarkPageAsChanged(control);
                }
                
                // Recursively track changes in nested controls
                if (childControl.Controls.Count > 0)
                {
                    TrackChangesInControls(childControl);
                }
            }
        }

        private void MarkPageAsChanged(Control control)
        {
            // Don't track changes during initial loading
            if (_isLoading) return;
            
            // Find the parent OptionsPage
            Control current = control;
            while (current != null && !(current is OptionsPage))
            {
                current = current.Parent;
            }
            
            if (current is OptionsPage page)
            {
                page.HasChanges = true;
            }
        }

        private void ClearChangeFlags()
        {
            foreach (OptionsPage page in _optionPages)
            {
                page.HasChanges = false;
            }
        }
    }
}