using System;
using System.Windows.Forms;
using mRemoteNG.App;
using NUnit.Framework;

namespace mRemoteNGTests.App
{
    public class DpiModeSelectorTests
    {
        [Test]
        public void NoOverride_ReturnsDefault()
        {
            Assert.That(DpiModeSelector.GetMode(null), Is.EqualTo(DpiModeSelector.DefaultMode));
            Assert.That(DpiModeSelector.GetMode(""), Is.EqualTo(DpiModeSelector.DefaultMode));
            Assert.That(DpiModeSelector.GetMode("   "), Is.EqualTo(DpiModeSelector.DefaultMode));
        }

        [TestCase("SystemAware", HighDpiMode.SystemAware)]
        [TestCase("systemaware", HighDpiMode.SystemAware)]
        [TestCase(" PerMonitorV2 ", HighDpiMode.PerMonitorV2)]
        [TestCase("DpiUnaware", HighDpiMode.DpiUnaware)]
        [TestCase("DpiUnawareGdiScaled", HighDpiMode.DpiUnawareGdiScaled)]
        public void KnownModes_AreParsedIgnoringCase(string requested, HighDpiMode expected)
        {
            Assert.That(DpiModeSelector.GetMode(requested), Is.EqualTo(expected));
        }

        [Test]
        public void UnknownMode_FallsBackToDefault()
        {
            Assert.That(DpiModeSelector.GetMode("nonsense"), Is.EqualTo(DpiModeSelector.DefaultMode));
            Assert.That(DpiModeSelector.GetMode("12345"), Is.EqualTo(DpiModeSelector.DefaultMode));
        }
        [Test]
        public void DefaultMode_IsSystemAware()
        {
            // Restores the layout of the .NET 9 build, which the user prefers over the clearly
            // larger PerMonitorV2 interface from upstream.
            Assert.That(DpiModeSelector.DefaultMode, Is.EqualTo(HighDpiMode.SystemAware));
            Assert.That(DpiModeSelector.GetMode(null), Is.EqualTo(HighDpiMode.SystemAware));
        }

        [Test]
        public void PerMonitorV2_CanStillBeOptedInto()
        {
            Assert.That(DpiModeSelector.GetMode("PerMonitorV2"), Is.EqualTo(HighDpiMode.PerMonitorV2));
        }
    }
}