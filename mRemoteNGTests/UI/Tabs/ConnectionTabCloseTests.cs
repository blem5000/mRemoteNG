using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using mRemoteNG.Connection;
using mRemoteNG.UI.Tabs;
using NUnit.Framework;

namespace mRemoteNGTests.UI.Tabs
{
    /// <summary>
    /// A connection tab keeps the <see cref="ConnectionInfo"/> in <c>Tag</c> until the protocol
    /// attaches its <see cref="InterfaceControl"/>. Closing such a tab used to throw
    /// <c>InvalidCastException</c> from <c>ConnectionTab.OnFormClosing</c> on the UI thread.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public class ConnectionTabCloseTests
    {
        private static void RaiseFormClosing(ConnectionTab tab)
        {
            MethodInfo onFormClosing = typeof(ConnectionTab)
                .GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(onFormClosing, Is.Not.Null, "ConnectionTab.OnFormClosing not found");

            onFormClosing.Invoke(tab, new object[]
            {
                new FormClosingEventArgs(CloseReason.UserClosing, false)
            });
        }

        [Test]
        public void OnFormClosing_WhenConnectionIsStillAttaching_DoesNotThrow()
        {
            using ConnectionTab tab = new()
            {
                silentClose = true,
                Tag = new ConnectionInfo()
            };

            Assert.DoesNotThrow(() => RaiseFormClosing(tab));
        }

        [Test]
        public void OnFormClosing_WithoutAnyTag_DoesNotThrow()
        {
            using ConnectionTab tab = new() { silentClose = true, Tag = null };

            Assert.DoesNotThrow(() => RaiseFormClosing(tab));
        }
    }
}
