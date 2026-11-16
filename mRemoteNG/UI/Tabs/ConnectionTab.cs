using System;
using System.Windows.Forms;
using mRemoteNG.App;
using mRemoteNG.App.Info;
using mRemoteNG.Config;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.VNC;
using mRemoteNG.Properties;
using mRemoteNG.UI.TaskDialog;
using WeifenLuo.WinFormsUI.Docking;
using mRemoteNG.Resources.Language;
using System.Runtime.Versioning;

namespace mRemoteNG.UI.Tabs
{
    [SupportedOSPlatform("windows")]
    public partial class ConnectionTab : DockContent
    {
        /// <summary>
        ///Silent close ignores the popup asking for confirmation
        /// </summary>
        public bool silentClose { get; set; }

        /// <summary>
        /// Protocol close ignores the interface controller cleanup and the user confirmation dialog
        /// </summary>
        public bool protocolClose { get; set; }

        public ConnectionTab()
        {
            InitializeComponent();
            GotFocus += ConnectionTab_GotFocus;
        }

        private void ConnectionTab_GotFocus(object sender, EventArgs e)
        {
            TabHelper.Instance.CurrentTab = this;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Answering "No" to the confirmation must leave everything exactly as it was,
            // including which tab is active. Showing the modal dialog deactivates this tab and
            // the dock panel then activates another document (typically the tab of the other
            // panel), so the user's tab lost the focus even though the close was cancelled.
            bool wasActiveDocument = IsActiveDocument();

            if (!protocolClose)
            {
                if (!silentClose)
                {
                    if (Settings.Default.ConfirmCloseConnection == (int)ConfirmCloseEnum.All)
                    {
                        DialogResult result = CTaskDialog.MessageBox(this, GeneralAppInfo.ProductName,
                                                            string
                                                                .Format(Language.ConfirmCloseConnectionPanelMainInstruction,
                                                                        TabText), "", "", "",
                                                            Language.CheckboxDoNotShowThisMessageAgain,
                                                            ETaskDialogButtons.YesNo, ESysIcons.Question,
                                                            ESysIcons.Question);
                        if (CTaskDialog.VerificationChecked)
                        {
                            Settings.Default.ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
                            Settings.Default.Save();
                        }

                        if (result == DialogResult.No)
                        {
                            e.Cancel = true;
                        }
                        else
                        {
                            CloseAttachedProtocol();
                        }
                    }
                    else
                    {
                        // close without the confirmation prompt...
                        CloseAttachedProtocol();
                    }
                }
                else
                {
                    CloseAttachedProtocol();
                }
            }

            base.OnFormClosing(e);

            if (e.Cancel && wasActiveDocument)
                RestoreAsActiveDocument();
        }

        /// <summary>
        /// True when this tab is the one currently shown in its panel.
        /// </summary>
        private bool IsActiveDocument()
        {
            try
            {
                return DockHandler != null && DockHandler.IsActivated;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Brings the focus back to this tab after a cancelled close.
        ///
        /// Done after the close sequence finished (BeginInvoke), because the dock panel picks the
        /// next active document while the modal dialog is up and would overwrite an activation
        /// performed earlier.
        /// </summary>
        private void RestoreAsActiveDocument()
        {
            if (!IsHandleCreated || IsDisposed || Disposing) return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing || !IsHandleCreated) return;
                    if (DockHandler == null || DockHandler.DockPanel == null) return;

                    DockHandler.Activate();
                }));
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage(
                    "ConnectionTab.RestoreAsActiveDocument() failed", ex);
            }
        }


        #region HelperFunctions  

        /// <summary>
        /// Closes the protocol attached to this tab, if there is one.
        /// While a connection is still being established the tag holds the
        /// <see cref="ConnectionInfo"/> instead of an <see cref="InterfaceControl"/>,
        /// so closing such a tab must not blow up on the cast.
        /// </summary>
        private void CloseAttachedProtocol()
        {
            (Tag as InterfaceControl)?.Protocol.Close();
        }

        public void RefreshInterfaceController()
        {
            try
            {
                InterfaceControl interfaceControl = Tag as InterfaceControl;
                if (interfaceControl?.Info.Protocol == ProtocolType.VNC)
                    ((ProtocolVNC)interfaceControl.Protocol).RefreshScreen();
            }
            catch (Exception ex)
            {
                Runtime.MessageCollector.AddExceptionMessage("RefreshIC (UI.Window.Connection) failed", ex);
            }
        }

        #endregion
    }
}