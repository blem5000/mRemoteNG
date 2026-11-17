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
        public void DefaultMode_MatchesUpstream()
        {
            // Keep upstream's default until the right value is confirmed on the user's display
            // scaling; the mode can be overridden with MREMOTENG_DPI_MODE without a rebuild.
            Assert.That(DpiModeSelector.DefaultMode, Is.EqualTo(HighDpiMode.PerMonitorV2));
            Assert.That(DpiModeSelector.GetMode(null), Is.EqualTo(HighDpiMode.PerMonitorV2));
        }

        [Test]
        public void AllModes_CanBeOptedInto()
        {
            Assert.That(DpiModeSelector.GetMode("SystemAware"), Is.EqualTo(HighDpiMode.SystemAware));
            Assert.That(DpiModeSelector.GetMode("PerMonitorV2"), Is.EqualTo(HighDpiMode.PerMonitorV2));
            Assert.That(DpiModeSelector.GetMode("DpiUnaware"), Is.EqualTo(HighDpiMode.DpiUnaware));
            Assert.That(DpiModeSelector.GetMode("DpiUnawareGdiScaled"), Is.EqualTo(HighDpiMode.DpiUnawareGdiScaled));
        }
    }
}