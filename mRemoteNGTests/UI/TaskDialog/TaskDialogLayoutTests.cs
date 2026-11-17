using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using mRemoteNG.Resources.Language;
using mRemoteNG.UI.TaskDialog;
using NUnit.Framework;

namespace mRemoteNGTests.UI.TaskDialog
{
    [TestFixture]
    public class TaskDialogLayoutTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(20);

        /// <summary>
        /// The verification checkbox must not share a row with the Yes/No buttons otherwise
        /// a long text ("Do not show this message again") runs underneath them and is clipped.
        /// </summary>
        [Test]
        public void VerificationCheckbox_HasItsOwnRowBelowTheButtons() => RunOnStaThread(() =>
        {
            using frmTaskDialog dialog = new()
            {
                Title = "mRemoteNG Connection Manager",
                MainInstruction = "Are you sure you want to close the panel, \"MA1 BACKBONE\"? Any connections that it contains will also be closed.",
                Buttons = ETaskDialogButtons.YesNo,
                VerificationText = Language.CheckboxDoNotShowThisMessageAgain,
                Width = 520
            };
            dialog.BuildForm();
            dialog.Show();
            _ = dialog.Handle;
            dialog.PerformLayout();
            Application.DoEvents();

            Control buttons = FindControl(dialog, "pnlButtons");
            Control checkbox = FindControl(dialog, "cbVerify");
            Control yes = FindControl(buttons, "bt2");

            Assert.That(checkbox.Visible, Is.True, "The verification checkbox should be visible");
            Assert.That(checkbox.Dock, Is.EqualTo(DockStyle.None),
                        "The checkbox must not stretch across the button row");
            Assert.That(checkbox.Top, Is.GreaterThanOrEqualTo(yes.Bottom),
                        "The checkbox must be on its own row below the Yes button");
            Assert.That(checkbox.Height, Is.GreaterThan(0));
            Assert.That(checkbox.Width, Is.GreaterThanOrEqualTo(dialog.LogicalToDeviceUnits(300)),
                        "The checkbox must be wide enough for a long verification text");
        });

        private static Control FindControl(Control parent, string name)
        {
            foreach (Control c in parent.Controls)
            {
                if (c.Name == name)
                    return c;
                Control found = FindControl(c, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void RunOnStaThread(Action action)
        {
            Exception caught = null;
            using var ready = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                using Form pump = new();
                _ = pump.Handle;
                ready.Set();
                pump.BeginInvoke(new Action(() =>
                {
                    try { action(); }
                    catch (Exception ex) { caught = ex; }
                    finally { Application.ExitThread(); }
                }));
                Application.Run(new ApplicationContext());
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            if (!thread.Join(TestTimeout))
                Assert.Fail("STA test thread timed out");
            if (caught != null)
                throw caught;
        }
    }
}
