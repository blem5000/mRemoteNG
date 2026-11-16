using System;
using System.IO;
using System.Windows.Forms;
using mRemoteNG.App.Info;
using NUnit.Framework;

namespace mRemoteNGTests.App.Info;

public class UserSettingsFileTests
{
    [Test]
    public void PortableFileLivesNextToTheExecutable()
    {
        Assert.That(Path.GetFileName(UserSettingsFile.PortablePath), Is.EqualTo("user.config"));
        Assert.That(Path.GetDirectoryName(UserSettingsFile.PortablePath),
                    Is.EqualTo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
    }

    [Test]
    public void StandardCopyLivesInAppData()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.That(UserSettingsFile.StandardPath, Does.StartWith(appData));
        Assert.That(Path.GetFileName(UserSettingsFile.StandardPath), Is.EqualTo("user.config"));
        Assert.That(Path.GetDirectoryName(UserSettingsFile.StandardPath),
                    Does.EndWith(Application.ProductName));
    }

    [Test]
    public void StandardCopyIsIndependentOfTheProgramFolder()
    {
        // The whole point: the stable copy must not contain the (hashed) executable path.
        Assert.That(UserSettingsFile.StandardPath, Does.Not.Contain("_Url_"));
        Assert.That(UserSettingsFile.StandardPath, Does.Not.Contain(AppDomain.CurrentDomain.BaseDirectory));
    }

    [Test]
    public void RuntimeFileIsTheOneUsedByTheSettingsSystem()
    {
        Assert.That(Path.GetFileName(UserSettingsFile.RuntimePath), Is.EqualTo("user.config"));
    }
}
