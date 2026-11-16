using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteNG.Config;
using mRemoteNG.Themes;
using mRemoteNG.UI.Tabs;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace mRemoteNGTests.UI.Tabs
{
    /// <summary>
    /// Regression tests for the focus restore after a cancelled close.
    ///
    /// Answering "No" to the "close this connection?" question has to leave the interface exactly
    /// as it was, including which tab is active. Showing the dialog takes the activation away from
    /// the tab and the docking library activates another document, which the user saw as
    /// "the active tab jumps to the device on the left".
    ///
    /// No dialog is ever displayed here: the confirmation setting is forced to Never, and the
    /// moment the dialog takes the focus away is simulated by a FormClosing handler that activates
    /// the other tab. That runs at the same point in the sequence, so the tests still cover the
    /// real ordering without popping up a modal window.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public class ConnectionTabFocusRestoreTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private int _savedConfirm;
        private object _settings;

        private object SettingsDefault => _settings ??= LoadSettings();

        private static object LoadSettings()
        {
            Type type = typeof(ConnectionTab).Assembly.GetType("mRemoteNG.Properties.Settings");
            Assert.That(type, Is.Not.Null, "mRemoteNG.Properties.Settings not found");

            object instance = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                                  ?.GetValue(null);
            Assert.That(instance, Is.Not.Null, "Settings.Default not found");
            return instance;
        }

        private int ConfirmClose
        {
            get => (int)SettingsDefault.GetType().GetProperty("ConfirmCloseConnection").GetValue(SettingsDefault);
            set => SettingsDefault.GetType().GetProperty("ConfirmCloseConnection").SetValue(SettingsDefault, value);
        }

        [SetUp]
        public void DisableDialogs()
        {
            _savedConfirm = ConfirmClose;
            ConfirmClose = (int)ConfirmCloseEnum.Never;
        }

        [TearDown]
        public void RestoreDialogs() => ConfirmClose = _savedConfirm;

        private static void Pump()
        {
            for (int i = 0; i < 25; i++)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// Dock panels refuse to show documents without a theme. The application loads it from
        /// vs2015light.vstheme; this builds the same type directly so no theme file is required.
        /// </summary>
        private static ThemeBase CreateTheme()
        {
            string themeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "vs2015light.vstheme");
            if (File.Exists(themeFile))
                return new MremoteNGThemeBase(File.ReadAllBytes(themeFile));

            Assembly docking = typeof(DockPanel).Assembly;
            Type builtIn = docking.GetType("WeifenLuo.WinFormsUI.Docking.VS2015LightTheme");
            if (builtIn != null && Activator.CreateInstance(builtIn) is ThemeBase theme)
                return theme;

            Assert.Ignore("No DockPanel theme available in this environment");
            return null;
        }

        private sealed class TabHost : IDisposable
        {
            public Form Form { get; }
            public DockPanel Panel { get; }
            public ConnectionTab First { get; }
            public ConnectionTab Second { get; }

            public TabHost()
            {
                Form = new Form { Width = 900, Height = 600, ShowInTaskbar = false };
                Panel = new DockPanel { Dock = DockStyle.Fill, Theme = CreateTheme() };
                Form.Controls.Add(Panel);
                Form.Show();
                Pump();

                First = new ConnectionTab { TabText = "LEFTDEVICE" };
                Second = new ConnectionTab { TabText = "RIGHTDEVICE" };
                First.Show(Panel, DockState.Document);
                Second.Show(Panel, DockState.Document);
                Pump();
            }

            public void Dispose()
            {
                First.Dispose();
                Second.Dispose();
                Panel.Dispose();
                Form.Dispose();
            }
        }

        private static void RaiseClosing(ConnectionTab tab, bool cancel)
        {
            MethodInfo onFormClosing = typeof(ConnectionTab).GetMethod("OnFormClosing", Private);
            Assert.That(onFormClosing, Is.Not.Null, "ConnectionTab.OnFormClosing not found");

            FormClosingEventArgs args = new(CloseReason.UserClosing, false) { Cancel = cancel };
            onFormClosing.Invoke(tab, new object[] { args });
        }

        [Test]
        public void CancelledClose_BringsTheFocusBackToTheTabThatWasBeingClosed()
        {
            using TabHost host = new();

            // The user works in the right-hand tab and clicks its "X".
            host.Second.DockHandler.Activate();
            Pump();
            Assume.That(host.Second.DockHandler.IsActivated, Is.True, "could not activate the second tab");

            // Simulate the confirmation dialog taking the activation away. The handler runs at the
            // same point as the dialog does, right before the cancel decision is applied.
            void StealFocusDuringClose(object sender, FormClosingEventArgs e) => host.First.DockHandler.Activate();
            host.Second.FormClosing += StealFocusDuringClose;

            try
            {
                RaiseClosing(host.Second, cancel: true);
            }
            finally
            {
                host.Second.FormClosing -= StealFocusDuringClose;
            }

            Pump();

            Assert.That(host.Second.DockHandler.IsActivated, Is.True,
                        "the tab whose close was cancelled must be active again");
            Assert.That(host.First.DockHandler.IsActivated, Is.False,
                        "the other tab must not keep the focus");
        }

        [Test]
        public void CancelledClose_DoesNotDisposeTheTab()
        {
            using TabHost host = new();
            host.Second.DockHandler.Activate();
            Pump();

            RaiseClosing(host.Second, cancel: true);
            Pump();

            Assert.That(host.Second.IsDisposed, Is.False);
            Assert.That(host.Second.TabText, Is.EqualTo("RIGHTDEVICE"));
        }

        [Test]
        public void CancelledClose_OfAnInactiveTab_DoesNotStealFocus()
        {
            using TabHost host = new();

            // Focus stays on the first tab; the second one is closed in the background.
            host.First.DockHandler.Activate();
            Pump();
            Assume.That(host.First.DockHandler.IsActivated, Is.True);

            RaiseClosing(host.Second, cancel: true);
            Pump();

            Assert.That(host.First.DockHandler.IsActivated, Is.True,
                        "a cancelled close of an inactive tab must not move the focus");
        }

        [Test]
        public void RestoreAsActiveDocument_ReactivatesTheTabAfterFocusWasTaken()
        {
            using TabHost host = new();

            host.Second.DockHandler.Activate();
            Pump();
            host.First.DockHandler.Activate();
            Pump();
            Assert.That(host.First.DockHandler.IsActivated, Is.True, "precondition: focus moved away");

            MethodInfo restore = typeof(ConnectionTab).GetMethod("RestoreAsActiveDocument", Private);
            Assert.That(restore, Is.Not.Null, "ConnectionTab.RestoreAsActiveDocument not found");
            restore.Invoke(host.Second, null);
            Pump();

            Assert.That(host.Second.DockHandler.IsActivated, Is.True);
        }
    }
}
