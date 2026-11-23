using System;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;


namespace mRemoteNG.Security.KeyDerivation
{
    public class Pkcs5S2KeyGenerator : IKeyDerivationFunction
    {
        public const int DefaultIterations = 1000;
        // A crafted connection file could set KdfIterations to int.MaxValue and hang the UI thread
        // for hours in PBKDF2. Cap the value read from any untrusted XML to a sane upper bound.
        public const int MaxIterations = 1_000_000;

        private readonly int _iterations;
        private readonly int _keyBitSize;

        /// <summary>
        /// Bounds an iteration count read from an untrusted source. Out-of-range values fall back
        /// to the default instead of being used verbatim.
        /// </summary>
        public static int ClampIterations(int iterations) =>
            iterations >= DefaultIterations && iterations <= MaxIterations ? iterations : DefaultIterations;

        public Pkcs5S2KeyGenerator(int keyBitSize = 256, int iterations = 1000)
        {
            if (iterations < 1000)
                throw new ArgumentOutOfRangeException($"Minimum value of {nameof(iterations)} is 1000");
            if (keyBitSize < 0)
                throw new ArgumentOutOfRangeException($"{nameof(keyBitSize)} must be positive");
            _keyBitSize = keyBitSize;
            _iterations = iterations;
        }

        public byte[] DeriveKey(string password, byte[] salt)
        {
            byte[] passwordInBytes = PbeParametersGenerator.Pkcs5PasswordToBytes(password.ToCharArray());

            Pkcs5S2ParametersGenerator keyGenerator = new();
            keyGenerator.Init(passwordInBytes, salt, _iterations);

            KeyParameter keyParameter = (KeyParameter)keyGenerator.GenerateDerivedMacParameters(_keyBitSize);
            byte[] keyBytes = keyParameter.GetKey();
            return keyBytes;
        }
    }
}