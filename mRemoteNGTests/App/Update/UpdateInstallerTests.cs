using mRemoteNG.App.Update;
using NUnit.Framework;

namespace mRemoteNGTests.App.Update;

public class UpdateInstallerTests
{
    [Test]
    public void GeneratedScriptWaitsForTheOldProcessAndCopiesFiles()
    {
        string script = UpdateInstaller.BuildScript(
            processId: 4321,
            stagingDirectory: @"C:\Temp\stage",
            targetDirectory: @"C:\Apps\mRemoteNG",
            executablePath: @"C:\Apps\mRemoteNG\mRemoteNG.exe",
            logPath: @"C:\Temp\update.log",
            updateFilePath: @"C:\Temp\update.zip");

        Assert.That(script, Does.Contain("Get-Process -Id 4321"));
        Assert.That(script, Does.Contain("WaitForExit"));
        Assert.That(script, Does.Contain("'C:\\Temp\\stage'"));
        Assert.That(script, Does.Contain("Copy-Item"));
        Assert.That(script, Does.Contain("Start-Process -FilePath 'C:\\Apps\\mRemoteNG\\mRemoteNG.exe'"));
        Assert.That(script, Does.Contain("'C:\\Temp\\update.log'"));
        // The downloaded package and the staging folder are cleaned up afterwards.
        Assert.That(script, Does.Contain("'C:\\Temp\\update.zip'"));
    }

    [Test]
    public void GeneratedScriptEscapesSingleQuotesInPaths()
    {
        string script = UpdateInstaller.BuildScript(
            processId: 1,
            stagingDirectory: @"C:\O'Brien\stage",
            targetDirectory: @"C:\O'Brien\app",
            executablePath: @"C:\O'Brien\app\mRemoteNG.exe",
            logPath: @"C:\O'Brien\update.log",
            updateFilePath: @"C:\O'Brien\update.zip");

        Assert.That(script, Does.Contain("'C:\\O''Brien\\stage'"));
        Assert.That(script, Does.Not.Contain("'C:\\O'Brien\\stage'"));
    }
}
