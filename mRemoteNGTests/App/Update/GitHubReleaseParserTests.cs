using System;
using mRemoteNG.App.Update;
using NUnit.Framework;

namespace mRemoteNGTests.App.Update;

public class GitHubReleaseParserTests
{
    private const string FrameworkDependentSuffix = "-x64.zip";
    private const string SelfContainedSuffix = "-x64-selfcontained.zip";

    private const string ReleasesJson = """
        [
          {
            "tag_name": "20261117-v1.78.2-NB",
            "draft": true,
            "prerelease": false,
            "body": "draft release",
            "assets": [
              {
                "name": "mRemoteNG-1.78.3-fix-20261117-x64.zip",
                "browser_download_url": "https://example.com/draft.zip"
              }
            ]
          },
          {
            "tag_name": "20261116-v1.78.2-NB",
            "draft": false,
            "prerelease": false,
            "body": "Polish release notes",
            "assets": [
              {
                "name": "mRemoteNG-1.78.2-fix-20261116-x64-selfcontained.zip",
                "browser_download_url": "https://example.com/app-selfcontained.zip",
                "digest": "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
              },
              {
                "name": "mRemoteNG-1.78.2-fix-20261116-x64.zip",
                "browser_download_url": "https://example.com/app.zip",
                "digest": "sha256:DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF"
              }
            ]
          }
        ]
        """;

    [Test]
    public void RejectsNonHttpsDownloadUrl()
    {
        string json = """
            [
              {
                "tag_name": "20261117-v1.78.2-NB",
                "draft": false,
                "prerelease": false,
                "body": "",
                "assets": [
                  {
                    "name": "mRemoteNG-1.78.2-fix-20261117-x64.zip",
                    "browser_download_url": "http://example.com/app.zip"
                  }
                ]
              }
            ]
            """;

        Assert.That(GitHubReleaseParser.ParseLatestRelease(json, false, FrameworkDependentSuffix), Is.Null,
                    "An update URL without HTTPS must not be accepted");
    }

    [Test]
    public void UpdateInfoRequiresSha256Checksum()
    {
        UpdateInfo without = UpdateInfo.FromGitHubRelease(
            "20261117-v1.78.2-NB", new Version(1, 78, 2), "app-x64.zip",
            new Uri("https://example.com/app.zip"), "", string.Empty);
        Assert.That(without.IsValid, Is.False,
                    "An update without a SHA-256 digest must be rejected (checksum would be skipped)");

        UpdateInfo with = UpdateInfo.FromGitHubRelease(
            "20261117-v1.78.2-NB", new Version(1, 78, 2), "app-x64.zip",
            new Uri("https://example.com/app.zip"), "",
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
        Assert.That(with.IsValid, Is.True);
    }

    [Test]
    public void UpdateInfoStripsPathFromAssetName()
    {
        UpdateInfo info = UpdateInfo.FromGitHubRelease(
            "20261117-v1.78.2-NB", new Version(1, 78, 2),
            "..\\..\\evil-x64.zip", new Uri("https://example.com/app.zip"), "", "sha256:AABBCC");

        Assert.That(info.FileName, Is.EqualTo("evil-x64.zip"),
                    "A path traversal in the asset name must be reduced to the file name");
    }

    [Test]
    public void ParsesVersionFromDateTag()
    {
        Assert.That(GitHubReleaseParser.ParseVersion("20261116-v1.78.2-NB"), Is.EqualTo(new Version(1, 78, 2)));
    }

    [Test]
    public void ReturnsNullForTagWithoutVersion()
    {
        Assert.That(GitHubReleaseParser.ParseVersion("latest-build"), Is.Null);
    }

    [Test]
    public void PicksFrameworkDependentAssetFromLatestPublishedRelease()
    {
        UpdateInfo info = GitHubReleaseParser.ParseLatestRelease(ReleasesJson, false, FrameworkDependentSuffix);

        Assert.That(info, Is.Not.Null);
        Assert.That(info.BuildTag, Is.EqualTo("20261116-v1.78.2-NB"));
        Assert.That(info.Version, Is.EqualTo(new Version(1, 78, 2)));
        Assert.That(info.FileName, Is.EqualTo("mRemoteNG-1.78.2-fix-20261116-x64.zip"));
        Assert.That(info.DownloadAddress, Is.EqualTo(new Uri("https://example.com/app.zip")));
        Assert.That(info.ChangeLog, Is.EqualTo("Polish release notes"));
        Assert.That(info.Checksum, Is.EqualTo("DEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEFDEADBEEF"));
        Assert.That(info.IsValid, Is.True);
    }

    [Test]
    public void PicksSelfContainedAssetWhenThatIsTheRunningBuild()
    {
        UpdateInfo info = GitHubReleaseParser.ParseLatestRelease(ReleasesJson, false, SelfContainedSuffix);

        Assert.That(info, Is.Not.Null);
        Assert.That(info.FileName, Is.EqualTo("mRemoteNG-1.78.2-fix-20261116-x64-selfcontained.zip"));
        Assert.That(info.DownloadAddress, Is.EqualTo(new Uri("https://example.com/app-selfcontained.zip")));
    }

    [Test]
    public void SkipsDraftReleases()
    {
        UpdateInfo info = GitHubReleaseParser.ParseLatestRelease(ReleasesJson, true, FrameworkDependentSuffix);

        Assert.That(info, Is.Not.Null);
        Assert.That(info.BuildTag, Is.Not.EqualTo("20261117-v1.78.2-NB"));
    }

    [Test]
    public void ReturnsNullWhenNoAssetMatchesRunningBuild()
    {
        UpdateInfo info = GitHubReleaseParser.ParseLatestRelease(ReleasesJson, false, "-installer.msi");

        Assert.That(info, Is.Null);
    }

    [Test]
    public void ReturnsNullForGarbageInput()
    {
        Assert.That(GitHubReleaseParser.ParseLatestRelease(string.Empty, false, FrameworkDependentSuffix), Is.Null);
        Assert.That(GitHubReleaseParser.ParseLatestRelease("not json", false, FrameworkDependentSuffix), Is.Null);
    }

    [Test]
    public void PrereleaseIsIgnoredUnlessRequested()
    {
        const string json = """
            [
              {
                "tag_name": "20261116-v1.78.3-NB",
                "draft": false,
                "prerelease": true,
                "body": "",
                "assets": [
                  { "name": "mRemoteNG-1.78.3-fix-20261116-x64.zip", "browser_download_url": "https://example.com/pre.zip" }
                ]
              }
            ]
            """;

        Assert.That(GitHubReleaseParser.ParseLatestRelease(json, false, FrameworkDependentSuffix), Is.Null);
        Assert.That(GitHubReleaseParser.ParseLatestRelease(json, true, FrameworkDependentSuffix), Is.Not.Null);
    }
}
