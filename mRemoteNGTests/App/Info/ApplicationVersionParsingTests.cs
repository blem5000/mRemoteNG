using System;
using mRemoteNG.App.Info;
using mRemoteNG.App.Update;
using NUnit.Framework;

namespace mRemoteNGTests.App.Info;

public class ApplicationVersionParsingTests
{
    [Test]
    public void ReadsVersionFromInformationalString()
    {
        // The exact string the build produces and the About box shows.
        Version version = GeneralAppInfo.GetVersion("1.78.2 (Nightly Build 3225) x64");

        Assert.That(version, Is.EqualTo(new Version(1, 78, 2)));
    }

    [Test]
    public void DoesNotBoltThePlatformOntoTheVersion()
    {
        // The old implementation produced "1.78.2.x64", which does not parse, so callers
        // silently received null and every update check said "no update available".
        Version version = GeneralAppInfo.GetVersion("1.78.2 (Nightly Build 3225) x64");

        Assert.That(version, Is.Not.Null);
        Assert.That(version.ToString(), Does.Not.Contain("x64"));
    }

    [Test]
    public void ReadsBuildNumber()
    {
        Assert.That(GeneralAppInfo.GetBuildNumber("1.78.2 (Nightly Build 3225) x64"), Is.EqualTo(3225));
    }

    [Test]
    public void ReturnsNullBuildNumberWhenAbsent()
    {
        Assert.That(GeneralAppInfo.GetBuildNumber("1.78.2 x64"), Is.Null);
        Assert.That(GeneralAppInfo.GetBuildNumber(""), Is.Null);
        Assert.That(GeneralAppInfo.GetBuildNumber(null), Is.Null);
    }

    [Test]
    public void FallsBackToAssemblyVersionForUnparsableInput()
    {
        Version fallback = new(9, 8, 7, 6);

        Assert.That(GeneralAppInfo.GetVersion("no version here", fallback), Is.EqualTo(fallback));
        Assert.That(GeneralAppInfo.GetVersion("", fallback), Is.EqualTo(fallback));
    }

    [Test]
    public void SupportsFourPartVersions()
    {
        Assert.That(GeneralAppInfo.GetVersion("1.78.2.3225 x64"), Is.EqualTo(new Version(1, 78, 2, 3225)));
    }

    [Test]
    public void ApplicationVersionIsAlwaysUsable()
    {
        Version version = GeneralAppInfo.GetApplicationVersion();

        Assert.That(version, Is.Not.Null);
        Assert.That(version.Major, Is.GreaterThan(0));
    }
}

public class ReleaseTagTests
{
    [Test]
    public void RebuildOnTheSameDayIsNewer()
    {
        Assert.That(ReleaseTag.IsNewer("20261116b-v1.78.2-NB", "20261116-v1.78.2-NB"), Is.True);
    }

    [Test]
    public void SameTagIsNotNewer()
    {
        Assert.That(ReleaseTag.IsNewer("20261116b-v1.78.2-NB", "20261116b-v1.78.2-NB"), Is.False);
    }

    [Test]
    public void OlderDayIsNotNewer()
    {
        Assert.That(ReleaseTag.IsNewer("20261116-v1.78.2-NB", "20261116b-v1.78.2-NB"), Is.False);
    }

    [Test]
    public void LaterDayIsNewer()
    {
        Assert.That(ReleaseTag.IsNewer("20261117-v1.78.2-NB", "20261116b-v1.78.2-NB"), Is.True);
    }

    [Test]
    public void SameTagComparisonIgnoresCase()
    {
        Assert.That(ReleaseTag.AreSame("20261116B-v1.78.2-NB", "20261116b-v1.78.2-NB"), Is.True);
    }

    [Test]
    public void UnknownShapeCountsAsDifferentBuild()
    {
        Assert.That(ReleaseTag.IsNewer("latest-build", "20261116b-v1.78.2-NB"), Is.True);
        Assert.That(ReleaseTag.GetSortKey("latest-build"), Is.Null);
    }

    [Test]
    public void RunningWithoutTagAlwaysSeesAnUpdate()
    {
        Assert.That(ReleaseTag.IsNewer("20261116b-v1.78.2-NB", null), Is.True);
        Assert.That(ReleaseTag.IsNewer("20261116b-v1.78.2-NB", ""), Is.True);
    }

    [Test]
    public void NoCandidateMeansNoUpdate()
    {
        Assert.That(ReleaseTag.IsNewer(null, "20261116b-v1.78.2-NB"), Is.False);
        Assert.That(ReleaseTag.IsNewer("", "20261116b-v1.78.2-NB"), Is.False);
    }

    [Test]
    public void SortKeyKeepsDateAndLowercasesSuffix()
    {
        Assert.That(ReleaseTag.GetSortKey("20261116B-v1.78.2-NB"), Is.EqualTo("20261116b"));
    }

    [Test]
    public void OrderingIsStableForAListOfRealReleases()
    {
        // Once every build is released its tag must be considered not-newer than itself
        // and older than the next one, which is what the whole update check hinges on.
        string[] chronological =
        [
            "20261110-v1.78.2-NB",
            "20261111-v1.78.2-NB",
            "20261116-v1.78.2-NB",
            "20261116b-v1.78.2-NB",
            "20261117-v1.78.2-NB"
        ];

        for (int i = 0; i < chronological.Length - 1; i++)
        {
            Assert.That(ReleaseTag.IsNewer(chronological[i + 1], chronological[i]), Is.True,
                        $"{chronological[i + 1]} should be newer than {chronological[i]}");
        }
    }
}
