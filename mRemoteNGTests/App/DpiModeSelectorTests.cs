using System;
using System.IO;
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
            Assert.That(DpiModeSelector.DefaultMode, Is.EqualTo(HighDpiMode.SystemAware));
            Assert.That(DpiModeSelector.GetMode(null), Is.EqualTo(HighDpiMode.SystemAware));
        }

        [Test]
        public void AllModes_CanBeOptedInto()
        {
            Assert.That(DpiModeSelector.GetMode("SystemAware"), Is.EqualTo(HighDpiMode.SystemAware));
            Assert.That(DpiModeSelector.GetMode("PerMonitorV2"), Is.EqualTo(HighDpiMode.PerMonitorV2));
            Assert.That(DpiModeSelector.GetMode("DpiUnaware"), Is.EqualTo(HighDpiMode.DpiUnaware));
            Assert.That(DpiModeSelector.GetMode("DpiUnawareGdiScaled"), Is.EqualTo(HighDpiMode.DpiUnawareGdiScaled));
        }

        private static string MakeConfig(string value)
        {
            string path = Path.Combine(Path.GetTempPath(), "dpi-" + Guid.NewGuid().ToString("N") + ".config");
            string xml = "<configuration><userSettings><mRemoteNG.Properties.OptionsAppearancePage>" +
                         "<setting name=\"DpiMode\" serializeAs=\"String\"><value>" + value + "</value></setting>" +
                         "</mRemoteNG.Properties.OptionsAppearancePage></userSettings></configuration>";
            File.WriteAllText(path, xml);
            return path;
        }

        [Test]
        public void ReadSettingFromXml_ParsesSavedValue()
        {
            string path = MakeConfig("DpiUnaware");
            try
            {
                Assert.That(DpiModeSelector.ReadSettingFromXml(path, DpiModeSelector.SettingName),
                            Is.EqualTo("DpiUnaware"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void GetConfiguredMode_UsesSavedValue()
        {
            string path = MakeConfig("SystemAware");
            try
            {
                Assert.That(DpiModeSelector.GetConfiguredMode(new[] { path }),
                            Is.EqualTo(HighDpiMode.SystemAware));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void GetConfiguredMode_FallsBackForMissingOrInvalidFile()
        {
            Assert.That(DpiModeSelector.GetConfiguredMode(new[] { Path.Combine(Path.GetTempPath(), "no-such-file") }),
                        Is.EqualTo(DpiModeSelector.DefaultMode));

            string path = MakeConfig("nonsense");
            try
            {
                Assert.That(DpiModeSelector.GetConfiguredMode(new[] { path }),
                            Is.EqualTo(DpiModeSelector.DefaultMode));
            }
            finally { File.Delete(path); }
        }
    }
}