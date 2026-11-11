using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using mRemoteNGTests.TestHelpers;
using System.Linq;

namespace mRemoteNGTests.UI.Forms
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsFormTests : OptionsFormSetupAndTeardown
    {
        // The form no longer closes itself: OK and Cancel ask whoever hosts it to close it.
        [Test]
        public void ClickingCancelButtonRequestsClose()
        {
            bool closeRequested = false;
            _optionsForm.CloseRequested += (o, e) => closeRequested = true;
            Button cancelButton = _optionsForm.FindControl<Button>("btnCancel");
            cancelButton.PerformClick();
            Assert.That(closeRequested, Is.True);
        }

        [Test]
        public void ClickingOKButtonRequestsClose()
        {
            bool closeRequested = false;
            _optionsForm.CloseRequested += (o, e) => closeRequested = true;
            Button okButton = _optionsForm.FindControl<Button>("btnOK");
            okButton.PerformClick();
            Assert.That(closeRequested, Is.True);
        }

        [Test]
        public void ListViewContainsOptionsPages()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items.Count, Is.EqualTo(12));
        }

        [Test]
        public void ChangingOptionMarksPageAsChanged()
        {
            var pnlMain = _optionsForm.FindControl<Panel>("pnlMain");
            Assert.That(pnlMain, Is.Not.Null);

            if (pnlMain.Controls.Count > 0)
            {
                var optionsPage = pnlMain.Controls[0] as mRemoteNG.UI.Forms.OptionsPages.OptionsPage;
                Assert.That(optionsPage, Is.Not.Null);

                // Find a checkbox in the options page (Controls.Find throws on an empty key)
                var checkBoxes = AllControls(optionsPage).OfType<CheckBox>().ToList();
                Assert.That(checkBoxes, Is.Not.Empty);

                var checkBox = checkBoxes[0];
                bool originalValue = checkBox.Checked;
                checkBox.Checked = !originalValue;
                Application.DoEvents();

                // Verify the page is marked as changed
                Assert.That(optionsPage.HasChanges, Is.True);
            }
        }

        private static IEnumerable<Control> AllControls(Control parent) =>
            parent.Controls.Cast<Control>().SelectMany(control => new[] { control }.Concat(AllControls(control)));
    }
}
