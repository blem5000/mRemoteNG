using System;
using System.IO;
using mRemoteNG.Plugins.SshTransfer;
using NUnit.Framework;

namespace mRemoteNGTests.Plugins;

[TestFixture]
public class KnownHostsStoreTests
{
    private string _tempPath;

    [SetUp]
    public void SetUp()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "mremoteng-knownhosts-" + Guid.NewGuid().ToString("N"));
        KnownHostsStore.StorePath = _tempPath;
    }

    [TearDown]
    public void TearDown()
    {
        KnownHostsStore.StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mRemoteNG", "ssh_known_hosts");
        if (File.Exists(_tempPath))
            File.Delete(_tempPath);
    }

    [Test]
    public void FirstUse_IsRememberedAndThenTrusted()
    {
        Assert.That(KnownHostsStore.CheckOrRemember("server.example", 22, "abc123"),
                    Is.EqualTo(KnownHostsStore.TrustResult.FirstUse));
        Assert.That(KnownHostsStore.CheckOrRemember("server.example", 22, "abc123"),
                    Is.EqualTo(KnownHostsStore.TrustResult.Trusted));
    }

    [Test]
    public void ChangedKey_IsRejected()
    {
        KnownHostsStore.CheckOrRemember("server.example", 22, "abc123");
        Assert.That(KnownHostsStore.CheckOrRemember("server.example", 22, "evil456"),
                    Is.EqualTo(KnownHostsStore.TrustResult.Changed));
    }

    [Test]
    public void DifferentPort_IsTrackedSeparately()
    {
        KnownHostsStore.CheckOrRemember("server.example", 22, "abc123");
        Assert.That(KnownHostsStore.CheckOrRemember("server.example", 2222, "other"),
                    Is.EqualTo(KnownHostsStore.TrustResult.FirstUse));
        Assert.That(KnownHostsStore.CheckOrRemember("server.example", 22, "abc123"),
                    Is.EqualTo(KnownHostsStore.TrustResult.Trusted));
    }
}
