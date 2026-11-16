using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteNG.Config;
using mRemoteNG.UI.Tabs;
using NUnit.Framework;

namespace mRemoteNGTests.UI.Tabs
{
    /// <summary>
    /// After a cancelled close ("No" in the confirmation dialog) the tab that was being closed
    /// must become the active one again. Showing the modal dialog deactivates it, and the dock
    /// panel then activates another document — usually the tab of the neighbouring panel, which
    /// made the focus jump to the other device.
    ///
    /// The tests never show the real confirmation dialog: it is a modal window, so it would block
    /// the test run (and ask whoever is at the machine to dismiss it). The setting is forced to
    /// <see cref="ConfirmCloseEnum.Never"/> for the duration of the fixture and restored afterwards.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public class ConnectionTabActivationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private int _savedConfirmCloseConnection;
        private object _settingsInstance;

        /// <summary>
        /// <c>mRemoteNG.Properties.Settings</c> is internal, so the test reaches it the same way
        /// the production code does — through its "Default" instance, obtained by reflection.
        /// </summary>
        private object SettingsDefault
        {
            get
            {
                if (_settingsInstance != null) return _settingsInstance;

                Type settingsType = typeof(ConnectionTab).Assembly.GetType("mRemoteNG.Properties.Settings");
                Assert.That(settingsType, Is.Not.Null, "mRemoteNG.Properties.Settings not found");

                _settingsInstance = settingsType
                    .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);

                Assert.That(_settingsInstance, Is.Not.Null, "Settings.Default not found");
                return _settingsInstance;
            }
        }

        private int ConfirmCloseConnection
        {
            get => (int)SettingsDefault.GetType()
                .GetProperty("ConfirmCloseConnection")
                .GetValue(SettingsDefault);
            set => SettingsDefault.GetType()
                .GetProperty("ConfirmCloseConnection")
                .SetValue(SettingsDefault, value);
        }

        [OneTimeSetUp]
        public void DisableConfirmationDialogs()
        {
            _savedConfirmCloseConnection = ConfirmCloseConnection;
            ConfirmCloseConnection = (int)ConfirmCloseEnum.Never;
        }

        [OneTimeTearDown]
        public void RestoreConfirmationDialogs()
        {
            ConfirmCloseConnection = _savedConfirmCloseConnection;
        }

        private static void PumpMessages()
        {
            for (int i = 0; i < 20; i++)
            {
                Application.DoEvents();
                Thread.Sleep(5);
            }
        }

        [Test]
        public void IsActiveDocument_IsFalseWithoutADockHandler()
        {
            // A tab that was never docked has no DockHandler; the check must not throw.
            using ConnectionTab tab = new() { TabText = "ORPHAN" };

            MethodInfo method = typeof(ConnectionTab).GetMethod("IsActiveDocument", Private);
            Assert.That(method, Is.Not.Null, "ConnectionTab.IsActiveDocument not found");

            Assert.That(method.Invoke(tab, null), Is.False);
        }

        [Test]
        public void RestoreAsActiveDocument_DoesNothingWithoutAHandle()
        {
            // No window handle means nothing to activate; calling it must be a harmless no-op.
            using ConnectionTab tab = new() { TabText = "NO HANDLE" };

            MethodInfo method = typeof(ConnectionTab).GetMethod("RestoreAsActiveDocument", Private);
            Assert.That(method, Is.Not.Null, "ConnectionTab.RestoreAsActiveDocument not found");

            Assert.DoesNotThrow(() => method.Invoke(tab, null));
            Assert.That(tab.IsHandleCreated, Is.False);
        }

        [Test]
        public void OnFormClosing_WhenCloseIsCancelled_KeepsTheTabAlive()
        {
            // A cancelled close must not detach the protocol or dispose the tab.
            using ConnectionTab tab = new() { TabText = "CANCELLED" };

            MethodInfo onFormClosing = typeof(ConnectionTab).GetMethod("OnFormClosing", Private);
            Assert.That(onFormClosing, Is.Not.Null);

            FormClosingEventArgs args = new(CloseReason.UserClosing, false);
            Assert.DoesNotThrow(() => onFormClosing.Invoke(tab, new object[] { args }));

            Assert.That(tab.IsDisposed, Is.False);
        }

        [Test]
        public void OnFormClosing_WithoutConfirmationPrompt_IsFast()
        {
            // Guards against a regression where a test drags in the modal dialog again:
            // with confirmation set to Never, closing must return immediately.
            using ConnectionTab tab = new() { TabText = "FAST" };
            MethodInfo onFormClosing = typeof(ConnectionTab).GetMethod("OnFormClosing", Private);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            FormClosingEventArgs args = new(CloseReason.UserClosing, false) { Cancel = true };
            onFormClosing.Invoke(tab, new object[] { args });
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                        "closing a tab must not block on a dialog");
        }

        [Test]
        public void NoModalDialogIsEverShown()
        {
            // Belt and braces: after all the closing work above, the test process must have no
            // window of its own, which is what a forgotten modal dialog would create.
            using ConnectionTab tab = new() { TabText = "CHECK" };
            MethodInfo onFormClosing = typeof(ConnectionTab).GetMethod("OnFormClosing", Private);
            onFormClosing.Invoke(tab, new object[] { new FormClosingEventArgs(CloseReason.UserClosing, false) });
            PumpMessages();

            Assert.That(Application.OpenForms.Count, Is.EqualTo(0),
                        "a modal dialog was left open by the close path");
        }
    }
}
