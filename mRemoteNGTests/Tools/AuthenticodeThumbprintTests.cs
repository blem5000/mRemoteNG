using System;
using System.IO;
using mRemoteNG.Tools;
using NUnit.Framework;

namespace mRemoteNGTests.Tools
{
    [TestFixture]
    public class AuthenticodeThumbprintTests
    {
        [Test]
        public void UnsignedFile_IsRejected()
        {
            string path = Path.Combine(Path.GetTempPath(), "mremoteng-unsigned-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(path, "not a signed executable");
            try
            {
                Authenticode auth = new(path)
                {
                    ThumbprintToMatch = "30E9AA926B036B2B81C80E5D0447347C21BC812D"
                };
                Assert.That(auth.VerifyThumbprintOnly(), Is.Not.EqualTo(Authenticode.StatusValue.Verified),
                            "An unsigned file must never pass the signature pin");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void SignedReleaseExecutable_MatchesPinnedThumbprint()
        {
            // The Release build is signed with our local self-signed release certificate during
            // packaging. When present, VerifyThumbprintOnly must accept its valid signature even
            // though the chain is untrusted.
            string exe = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "mRemoteNG", "bin", "x64", "Release", "mRemoteNG.exe");
            exe = Path.GetFullPath(exe);
            if (!File.Exists(exe))
                Assert.Ignore("Release build not available in this environment");

            Authenticode auth = new(exe)
            {
                ThumbprintToMatch = "30E9AA926B036B2B81C80E5D0447347C21BC812D"
            };
            Assert.That(auth.VerifyThumbprintOnly(), Is.EqualTo(Authenticode.StatusValue.Verified),
                        "A signed release must pass the pinned thumbprint check");
        }

        [Test]
        public void TamperedSignedExecutable_IsRejected()
        {
            // SigThief-style attack: the certificate blob alone must not be enough. Flipping a
            // byte in the DOS header changes the signed digest while leaving the embedded
            // certificate (and therefore its thumbprint) intact. WinVerifyTrust must catch this.
            string exe = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "mRemoteNG", "bin", "x64", "Release", "mRemoteNG.exe");
            exe = Path.GetFullPath(exe);
            if (!File.Exists(exe))
                Assert.Ignore("Release build not available in this environment");

            string tampered = Path.Combine(Path.GetTempPath(), "mremoteng-tampered-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(exe, tampered);
            try
            {
                byte[] bytes = File.ReadAllBytes(tampered);
                bytes[0x02] ^= 0x01; // e_cblp: covered by the Authenticode hash, ignored by the loader
                File.WriteAllBytes(tampered, bytes);

                Authenticode auth = new(tampered)
                {
                    ThumbprintToMatch = "30E9AA926B036B2B81C80E5D0447347C21BC812D"
                };
                Assert.That(auth.VerifyThumbprintOnly(), Is.Not.EqualTo(Authenticode.StatusValue.Verified),
                            "A file with a broken signed digest must be rejected even though its certificate thumbprint matches");
            }
            finally
            {
                File.Delete(tampered);
            }
        }

        [Test]
        public void MissingFile_IsRejected()
        {
            Authenticode auth = new(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N") + ".exe"))
            {
                ThumbprintToMatch = "30E9AA926B036B2B81C80E5D0447347C21BC812D"
            };
            Assert.That(auth.VerifyThumbprintOnly(), Is.EqualTo(Authenticode.StatusValue.FileNotExist));
        }

        [Test]
        public void EmptyThumbprint_IsRejected()
        {
            string path = Path.Combine(Path.GetTempPath(), "mremoteng-nothumb-" + Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(path, "x");
            try
            {
                Authenticode auth = new(path) { ThumbprintToMatch = string.Empty };
                Assert.That(auth.VerifyThumbprintOnly(), Is.EqualTo(Authenticode.StatusValue.NoThumbprintToMatch));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
