using System;
using System.Threading;
using mRemoteNG.Connection;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.UI.Tabs;
using mRemoteNG.UI.Window;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace mRemoteNGTests.UI.Window
{
    /// <summary>
    /// Regression tests for "Cannot access a disposed object. Object name: 'ConnectionWindow'.",
    /// which was raised from <c>ConnectionWindow.Prot_Event_Closed</c> (a synchronous Invoke on the
    /// closing panel) whenever a panel holding more than one connection was closed.
    /// </summary>
    [Apartment(ApartmentState.STA)]
    public class ConnectionWindowClosedTests
    {
        private sealed class StubProtocol : ProtocolBase
        {
            public StubProtocol() : base("stub")
            {
            }
        }

        private static InterfaceControl BuildInterfaceControl(out ConnectionTab tab, out StubProtocol protocol)
        {
            tab = new ConnectionTab();
            protocol = new StubProtocol();
            InterfaceControl interfaceControl = new(tab, protocol, new ConnectionInfo());
            tab.Tag = interfaceControl;
            return interfaceControl;
        }

        [Test]
        public void Prot_Event_Closed_DoesNotThrow_WhenPanelIsAlreadyDisposed()
        {
            ConnectionWindow window = new(new DockContent());
            BuildInterfaceControl(out _, out StubProtocol protocol);

            window.Dispose();

            Assert.DoesNotThrow(() => window.Prot_Event_Closed(protocol));
        }

        [Test]
        public void Prot_Event_Closed_IgnoresProtocolWithoutInterfaceControl()
        {
            ConnectionWindow window = new(new DockContent());
            StubProtocol protocol = new();

            try
            {
                Assert.DoesNotThrow(() => window.Prot_Event_Closed(protocol));
            }
            finally
            {
                window.Dispose();
            }
        }

        [Test]
        public void Close_WhenClosedSubscriberThrows_StillNotifiesRemainingSubscribers()
        {
            using StubProtocol protocol = new();
            using ManualResetEventSlim reachedSecondSubscriber = new();

            // this is exactly what ConnectionWindow.Prot_Event_Closed used to do once the panel
            // had already been disposed
            protocol.Closed += _ => throw new ObjectDisposedException("ConnectionWindow");
            protocol.Closed += _ => reachedSecondSubscriber.Set();

            Assert.DoesNotThrow(protocol.Close);
            Assert.That(reachedSecondSubscriber.Wait(TimeSpan.FromSeconds(10)), Is.True,
                        "the Closed event never reached the second subscriber");
        }
    }
}
