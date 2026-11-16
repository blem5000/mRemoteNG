using System;
using System.IO;
using System.Linq;
using mRemoteNG.DotNet.Update;
using NUnit.Framework;

namespace mRemoteNGTests.App.Checks;

public class DotNetRuntimeCheckTests
{
    private string _root;

    [SetUp]
    public void CreateFakeDotNetRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "mremoteng-dotnet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "shared", "Microsoft.WindowsDesktop.App"));
    }

    [TearDown]
    public void RemoveFakeDotNetRoot()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }

    private void AddDesktopRuntime(string version)
    {
        Directory.CreateDirectory(Path.Combine(_root, "shared", "Microsoft.WindowsDesktop.App", version));
    }

    [Test]
    public void ReturnsNullWhenNoDesktopRuntimeIsInstalled()
    {
        // This is exactly the self-contained user's machine: no .NET installed at all.
        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.Null);
    }

    [Test]
    public void ReturnsNullWhenOnlyAnOlderRuntimeIsInstalled()
    {
        // .NET 8 alone used to satisfy the old registry check and let the app crash later.
        AddDesktopRuntime("8.0.28");

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.Null);
    }

    [Test]
    public void FindsRequiredMajorVersion()
    {
        AddDesktopRuntime("9.0.0");
        AddDesktopRuntime("9.0.17");

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.EqualTo("9.0.17"));
    }

    [Test]
    public void AcceptsNewerMajorVersionsBecauseOfRollForward()
    {
        AddDesktopRuntime("8.0.28");
        AddDesktopRuntime("10.0.9");

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.EqualTo("10.0.9"));
    }

    [Test]
    public void IgnoresFoldersThatAreNotVersions()
    {
        AddDesktopRuntime("not-a-version");
        AddDesktopRuntime("9.0.5");

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.EqualTo("9.0.5"));
    }

    [Test]
    public void IgnoresMissingAndBrokenRoots()
    {
        string missing = Path.Combine(_root, "does-not-exist");
        AddDesktopRuntime("9.0.5");

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { null, "", missing, _root }, 9),
                    Is.EqualTo("9.0.5"));
    }

    [Test]
    public void CoreRuntimeWithoutDesktopRuntimeIsNotEnough()
    {
        Directory.CreateDirectory(Path.Combine(_root, "shared", "Microsoft.NETCore.App", "9.0.17"));

        Assert.That(DotNetRuntimeCheck.FindInstalledDesktopRuntime(new[] { _root }, 9), Is.Null);
    }

    [Test]
    public void DotNetRootsAreDeduplicatedAndNeverEmpty()
    {
        var roots = DotNetRuntimeCheck.GetDotNetRoots().ToList();

        Assert.That(roots, Is.Not.Empty);
        Assert.That(roots.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(roots.Count));
    }

    [Test]
    public void RequiredMajorVersionMatchesTheAnnouncedChannel()
    {
        Assert.That(DotNetRuntimeCheck.RequiredMajorVersion,
                    Is.EqualTo(int.Parse(DotNetRuntimeCheck.RequiredDotnetVersion.Split('.')[0])));
    }

    [Test]
    public void SelfContainedBuildIsDetectedFromCoreclrNextToTheExecutable()
    {
        // The framework-dependent build has no coreclr.dll next to the executable; the
        // self-contained one always does. Turn the file into the single source of truth.
        Assert.That(mRemoteNG.App.Runtime.IsSelfContainedBuild,
                    Is.EqualTo(File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "coreclr.dll"))));
    }
}
