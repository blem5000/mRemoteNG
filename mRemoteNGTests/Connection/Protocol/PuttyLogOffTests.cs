using System;
using System.Linq;
using mRemoteNG.Connection.Protocol;
using mRemoteNG.Connection.Protocol.RAW;
using mRemoteNG.Connection.Protocol.Rlogin;
using mRemoteNG.Connection.Protocol.Serial;
using mRemoteNG.Connection.Protocol.SSH;
using mRemoteNG.Connection.Protocol.Telnet;
using NUnit.Framework;

namespace mRemoteNGTests.Connection.Protocol
{
    [TestFixture]
    public class PuttyLogOffTests
    {
        private static readonly IntPtr SomeWindow = new(1);

        [Test]
        public void ShellProtocolsCanLogOffOnceTheirWindowExists()
        {
            // SSH, Telnet and rlogin end in a login shell that "exit" logs off.
            foreach (PuttyBase protocol in new PuttyBase[] { new ProtocolSSH2(), new ProtocolTelnet(), new ProtocolRlogin() })
            {
                Assert.That(protocol.CanLogOff, Is.False, $"{protocol.GetType().Name} before PuTTY started");
                protocol.PuttyHandle = SomeWindow;
                Assert.That(protocol.CanLogOff, Is.True, protocol.GetType().Name);
            }
        }

        [Test]
        public void UnsafeSshOptionsFromConnectionFileAreDropped()
        {
            var arguments = new mRemoteNG.Tools.Cmdline.CommandLineArguments();
            var method = typeof(PuttyBase).GetMethod("AddArgumentString",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method.Invoke(null, [arguments, "-proxycmd calc.exe -L 8080:host:80 -safeoption value"]);
            var result = arguments.ToArgumentList().ToList();

            Assert.That(result, Does.Not.Contain("-proxycmd"), "-proxycmd can execute a local command");
            Assert.That(result, Does.Not.Contain("-L"), "-L can forward ports");
            Assert.That(result, Does.Contain("-safeoption"), "harmless options must still work");
            Assert.That(result, Does.Contain("value"));
        }

        [Test]
        public void TryFocusForSendKeys_FailsWithoutARealWindow()
        {
            // SendKeys must never be used unless focus is confirmed. With no PuTTY window the
            // helper returns false so "exit"/OpeningCommand cannot go to an unrelated window.
            var protocol = new ProtocolSSH2();
            var method = typeof(PuttyBase).GetMethod("TryFocusForSendKeys",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "TryFocusForSendKeys must exist");
            Assert.That((bool)method.Invoke(protocol, null), Is.False);
        }

        [Test]
        public void RawAndSerialCannotLogOff()
        {
            // No shell of their own to end: "exit" would just be sent as data.
            foreach (PuttyBase protocol in new PuttyBase[] { new RawProtocol(), new ProtocolSerial() })
            {
                protocol.PuttyHandle = SomeWindow;
                Assert.That(protocol.CanLogOff, Is.False, protocol.GetType().Name);
            }
        }
    }
}
