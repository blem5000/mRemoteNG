using System;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using mRemoteNG.Config.Settings;
using NUnit.Framework;

namespace mRemoteNGTests.Config.Settings;

public class SettingsSaverTests
{
    private const string ThemePageSettingsType = "mRemoteNG.Properties.OptionsThemePage";

    /// <summary>
    /// The Options pages store their values in their own settings classes. Saving only
    /// Properties.Settings used to lose things like the selected theme and the update
    /// preference, so SaveOptionPages must write them to the user configuration.
    /// </summary>
    [Test]
    public void SaveOptionPagesPersistsOptionPageSettings()
    {
        (object provider, PropertyInfo themeNameProperty, string original) = GetThemeNameAccessors();
        string probeValue = original == "vs2015Dark" ? "vs2015Light" : "vs2015Dark";

        try
        {
            themeNameProperty.SetValue(provider, probeValue);
            SettingsSaver.SaveOptionPages();

            string userConfigPath = ConfigurationManager
                .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            XDocument userConfig = XDocument.Load(userConfigPath);
            XElement? themePageSection = userConfig
                .Descendants(ThemePageSettingsType)
                .FirstOrDefault();
            XElement? themeNameSetting = themePageSection?
                .Elements("setting")
                .FirstOrDefault(element => (string?)element.Attribute("name") == "ThemeName");

            Assert.That(themePageSection, Is.Not.Null, "OptionsThemePage section was not written to user.config");
            Assert.That(themeNameSetting?.Element("value")?.Value, Is.EqualTo(probeValue));
        }
        finally
        {
            themeNameProperty.SetValue(provider, original);
            SettingsSaver.SaveOptionPages();
        }
    }

    [Test]
    public void SaveOptionPagesSavesEveryOptionsPageProvider()
    {
        string[] expectedPages =
        [
            "OptionsAdvancedPage", "OptionsAppearancePage", "OptionsBackupPage", "OptionsConnectionsPage",
            "OptionsCredentialsPage", "OptionsDBsPage", "OptionsNotificationsPage", "OptionsSecurityPage",
            "OptionsStartupExitPage", "OptionsTabsPanelsPage", "OptionsThemePage", "OptionsUpdatesPage"
        ];

        foreach (string expectedPage in expectedPages)
        {
            Type? pageType = typeof(SettingsSaver).Assembly.GetType($"mRemoteNG.Properties.{expectedPage}");
            Assert.That(pageType, Is.Not.Null, $"{expectedPage} settings class is missing");
            Assert.That(pageType!.GetProperty("Default", BindingFlags.Public | BindingFlags.Static), Is.Not.Null,
                $"{expectedPage}.Default is missing");
        }
    }

    private static (object Provider, PropertyInfo ThemeNameProperty, string OriginalValue) GetThemeNameAccessors()
    {
        Type? pageType = typeof(SettingsSaver).Assembly.GetType(ThemePageSettingsType);
        Assert.That(pageType, Is.Not.Null, $"{ThemePageSettingsType} settings class is missing");

        object? provider = pageType!
            .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?
            .GetValue(null);
        Assert.That(provider, Is.Not.Null, $"{ThemePageSettingsType}.Default returned null");

        PropertyInfo? themeNameProperty = pageType.GetProperty("ThemeName");
        Assert.That(themeNameProperty, Is.Not.Null, $"{ThemePageSettingsType}.ThemeName is missing");

        string original = (string)themeNameProperty!.GetValue(provider)!;
        return (provider!, themeNameProperty, original);
    }
}
