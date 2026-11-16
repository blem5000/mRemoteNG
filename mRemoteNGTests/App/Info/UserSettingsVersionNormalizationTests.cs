using System;
using System.IO;
using System.Linq;
using mRemoteNG.App.Info;
using NUnit.Framework;

namespace mRemoteNGTests.App.Info;

public class UserSettingsVersionNormalizationTests
{
    private string _dir;
    private string _file;

    private const string SettingsTemplate = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
            <configSections>
                <sectionGroup name="userSettings" type="System.Configuration.UserSettingsGroup, System.Configuration.ConfigurationManager, Version={0}, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" >
                    <section name="mRemoteNG.Properties.Settings" type="System.Configuration.ClientSettingsSection, System.Configuration.ConfigurationManager, Version={0}, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" allowExeDefinition="MachineToLocalUser" requirePermission="false" />
                </sectionGroup>
            </configSections>
            <userSettings>
                <mRemoteNG.Properties.Settings>
                    <setting name="ThemeName" serializeAs="String">
                        <value>vs2015Dark</value>
                    </setting>
                </mRemoteNG.Properties.Settings>
        </userSettings>
        </configuration>
        """;

    [SetUp]
    public void CreateFile()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mremoteng-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "user.config");
    }

    [TearDown]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch (IOException) { }
    }

    /// <summary>
    /// A version that is never the running one, so the test works on .NET 9 and .NET 10 alike.
    /// </summary>
    private static string ForeignVersion =>
        UserSettingsFile.ConfigurationAssemblyVersion.Major == 10 ? "9.0.0.0" : "10.0.0.0";

    [Test]
    public void RewritesForeignRuntimeVersionToTheRunningOne()
    {
        // This is the file that crashed the self-contained (9.0.17) build: it was written
        // by a framework-dependent build running on a newer .NET major.
        string foreign = ForeignVersion;
        File.WriteAllText(_file, string.Format(SettingsTemplate, foreign));

        UserSettingsFile.NormalizeConfigurationVersion(_file);

        string expected = UserSettingsFile.ConfigurationAssemblyVersion.ToString();
        string content = File.ReadAllText(_file);

        Assert.That(content, Does.Not.Contain($"Version={foreign},"));
        Assert.That(content, Does.Contain($"Version={expected},"));
        // Both section declarations must be fixed, not just the first one.
        Assert.That(content.Split($"Version={expected},").Length - 1, Is.EqualTo(2));
    }

    [Test]
    public void LeavesMatchingVersionUntouched()
    {
        string expected = UserSettingsFile.ConfigurationAssemblyVersion.ToString();
        File.WriteAllText(_file, string.Format(SettingsTemplate, expected));
        byte[] before = File.ReadAllBytes(_file);

        UserSettingsFile.NormalizeConfigurationVersion(_file);

        Assert.That(File.ReadAllBytes(_file), Is.EqualTo(before));
    }

    [Test]
    public void KeepsAllSettingValues()
    {
        File.WriteAllText(_file, string.Format(SettingsTemplate, ForeignVersion));

        UserSettingsFile.NormalizeConfigurationVersion(_file);

        Assert.That(File.ReadAllText(_file), Does.Contain("<value>vs2015Dark</value>"));
    }

    [Test]
    public void MovesUnparsableFileAside()
    {
        File.WriteAllText(_file, "<configuration><configSections>");

        UserSettingsFile.NormalizeConfigurationVersion(_file);

        Assert.That(File.Exists(_file), Is.False);
        Assert.That(File.Exists(_file + ".invalid"), Is.True);
    }

    [Test]
    public void DoesNothingForMissingFile()
    {
        string missing = Path.Combine(_dir, "nope.config");

        Assert.DoesNotThrow(() => UserSettingsFile.NormalizeConfigurationVersion(missing));
        Assert.That(File.Exists(missing), Is.False);
    }

    [Test]
    public void IgnoresOtherAssembliesVersions()
    {
        // Only the configuration manager declarations are rewritten.
        string content = """
            <configuration>
              <configSections>
                <section name="other" type="Some.Other.Thing, Some.Other.Assembly, Version=1.2.3.4, Culture=neutral" />
              </configSections>
            </configuration>
            """;
        File.WriteAllText(_file, content);

        UserSettingsFile.NormalizeConfigurationVersion(_file);

        Assert.That(File.ReadAllText(_file), Does.Contain("Version=1.2.3.4"));
    }

    [Test]
    public void ConfigurationVersionComesFromTheLoadedAssembly()
    {
        Assert.That(UserSettingsFile.ConfigurationAssemblyVersion.Major, Is.GreaterThanOrEqualTo(9));
    }
}
