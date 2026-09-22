using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace MultiLaunch.Statics
{
    /// <summary>
    /// Cryptographic primitives used by the vault.
    ///
    /// Format v2 (current):
    ///   - Key derivation: PBKDF2-HMAC-SHA512 over the main password with a random per-vault salt.
    ///   - Sub-keys: HKDF-SHA256 splits the master key into a data key and a verification key,
    ///     so the value used to validate the password can never decrypt the stored secrets.
    ///   - Encryption: AES-256-GCM (authenticated). A fresh random nonce is generated for every
    ///     operation and the authentication tag makes any tampering detectable.
    ///   - Payload layout (Base64): [version:1][nonce:12][tag:16][ciphertext:n]
    ///
    /// Format v1 (legacy, decryption only): AES-256-CBC with key = SHA-256(password) and a static
    /// IV derived from the motherboard serial number. Kept solely to migrate existing databases.
    /// </summary>
    internal static class Crypto
    {
        public const int SaltSize = 32;
        public const int KeySize = 32;
        public const int NonceSize = 12;
        public const int TagSize = 16;

        /// <summary>Default PBKDF2 work factor. Stored alongside the vault so it can be raised later.</summary>
        public const int DefaultIterations = 600_000;

        private const byte FormatVersion = 2;

        /// <summary>Bound to every ciphertext as additional authenticated data.</summary>
        private static readonly byte[] AssociatedData = Encoding.ASCII.GetBytes("MultiLaunch.vault.v2");

        private static readonly byte[] DataKeyInfo = Encoding.ASCII.GetBytes("MultiLaunch.v2.data");
        private static readonly byte[] VerifyKeyInfo = Encoding.ASCII.GetBytes("MultiLaunch.v2.verify");

        #region Key derivation

        public static byte[] CreateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

        /// <summary>
        /// Stretches the main password into a master key. Deliberately slow: this is the only
        /// barrier protecting the vault if the SQLite file is stolen.
        /// </summary>
        public static byte[] DeriveMasterKey(string password, byte[] salt, int iterations)
        {
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(salt);

            if (iterations < 1)
                throw new ArgumentOutOfRangeException(nameof(iterations));

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA512, KeySize);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordBytes);
            }
        }

        /// <summary>Derives the key protecting the stored secrets.</summary>
        public static byte[] DeriveDataKey(byte[] masterKey)
            => HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, KeySize, salt: null, info: DataKeyInfo);

        /// <summary>Derives the key used only to validate the main password.</summary>
        public static byte[] DeriveVerificationKey(byte[] masterKey)
            => HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, KeySize, salt: null, info: VerifyKeyInfo);

        #endregion

        #region Authenticated encryption

        public static string Encrypt(byte[] key, string? plainText)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (key.Length != KeySize)
                throw new ArgumentException("Invalid key size.", nameof(key));

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            try
            {
                byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
                byte[] cipherBytes = new byte[plainBytes.Length];
                byte[] tag = new byte[TagSize];

                using (AesGcm aes = new(key, TagSize))
                {
                    aes.Encrypt(nonce, plainBytes, cipherBytes, tag, AssociatedData);
                }

                byte[] payload = new byte[1 + NonceSize + TagSize + cipherBytes.Length];
                payload[0] = FormatVersion;
                nonce.CopyTo(payload, 1);
                tag.CopyTo(payload, 1 + NonceSize);
                cipherBytes.CopyTo(payload, 1 + NonceSize + TagSize);

                return Convert.ToBase64String(payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        /// <summary>
        /// Decrypts a v2 payload. Returns <c>false</c> when the key is wrong or the data has been
        /// tampered with - a failure is never silently turned into an empty secret.
        /// </summary>
        public static bool TryDecrypt(byte[] key, string? payloadText, out string plainText)
        {
            plainText = string.Empty;

            if (key is null || key.Length != KeySize || string.IsNullOrEmpty(payloadText))
                return false;

            byte[] payload;
            try
            {
                payload = Convert.FromBase64String(payloadText);
            }
            catch (FormatException)
            {
                return false;
            }

            if (payload.Length < 1 + NonceSize + TagSize || payload[0] != FormatVersion)
                return false;

            byte[] nonce = payload.AsSpan(1, NonceSize).ToArray();
            byte[] tag = payload.AsSpan(1 + NonceSize, TagSize).ToArray();
            byte[] cipherBytes = payload.AsSpan(1 + NonceSize + TagSize).ToArray();
            byte[] plainBytes = new byte[cipherBytes.Length];

            try
            {
                using AesGcm aes = new(key, TagSize);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes, AssociatedData);

                plainText = Encoding.UTF8.GetString(plainBytes);
                return true;
            }
            catch (CryptographicException)
            {
                // Wrong key or altered ciphertext.
                return false;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        #endregion

        #region Legacy format (migration only)

        /// <summary>
        /// Decrypts a value written by the previous AES-CBC scheme. Returns <c>null</c> on failure.
        /// Only used once, to re-encrypt existing vaults with the current format.
        /// </summary>
        public static string? LegacyDecrypt(string password, string? encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return null;

            try
            {
                byte[] iv = LegacyIV();
                if (iv.Length != 16)
                    return null;

                using Aes aes = Aes.Create();
                aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                aes.IV = iv;

                using ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or InvalidOperationException)
            {
                return null;
            }
        }

        private static byte[] LegacyIV()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(LegacyDeviceId());
            return bytes.Length >= 16 ? bytes[..16] : bytes;
        }

        private static string LegacyDeviceId()
        {
            using System.Management.ManagementObjectSearcher searcher = new("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                return obj["SerialNumber"]?.ToString() ?? string.Empty;
            }

            return string.Empty;
        }

        #endregion

        /// <summary>Compares two strings without leaking their content through timing.</summary>
        public static bool FixedTimeEquals(string? left, string? right)
        {
            if (left is null || right is null)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(left),
                Encoding.UTF8.GetBytes(right));
        }

        public static SecureString? ConvertToSecureString(string? password)
        {
            if (string.IsNullOrEmpty(password))
                return null;

            SecureString securePassword = new();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            securePassword.MakeReadOnly();
            return securePassword;
        }
    }
}
