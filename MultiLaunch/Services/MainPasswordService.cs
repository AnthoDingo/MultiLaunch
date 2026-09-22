using MultiLaunch.Statics;
using System.Security.Cryptography;

namespace MultiLaunch.Services
{
    /// <summary>
    /// Holds the vault data key for the lifetime of the session.
    ///
    /// The main password itself is never kept: only the key derived from it, and that key stays
    /// DPAPI-protected in memory. It is unprotected for the duration of a single operation and
    /// zeroed immediately afterwards, so a memory dump of the running process does not hand over
    /// the vault. Callers go through <see cref="Encrypt"/> / <see cref="TryDecrypt"/> and never
    /// see the key material.
    /// </summary>
    public sealed class MainPasswordService : IDisposable
    {
        private readonly object _gate = new();
        private bool _disposed;
        private byte[]? _protectedKey;

        ~MainPasswordService() => Dispose(false);

        public bool IsUnlocked
        {
            get
            {
                lock (_gate)
                {
                    return _protectedKey != null;
                }
            }
        }

        /// <summary>
        /// Stores the data key for this session. The caller keeps ownership of <paramref name="dataKey"/>
        /// and is responsible for zeroing it.
        /// </summary>
        internal void SetDataKey(byte[] dataKey)
        {
            ArgumentNullException.ThrowIfNull(dataKey);

            if (dataKey.Length != Crypto.KeySize)
                throw new ArgumentException("Invalid key size.", nameof(dataKey));

            byte[] protectedKey = ProtectedData.Protect(dataKey, null, DataProtectionScope.CurrentUser);

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                ClearProtectedKey();
                _protectedKey = protectedKey;
            }
        }

        /// <summary>Encrypts a secret with the session data key.</summary>
        public string Encrypt(string? plainText)
        {
            byte[] key = UnprotectKey();
            try
            {
                return Crypto.Encrypt(key, plainText);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>
        /// Decrypts a stored secret. Returns <c>false</c> when the vault is locked, the payload is
        /// not in the expected format, or its authentication tag does not verify.
        /// </summary>
        public bool TryDecrypt(string? payload, out string plainText)
        {
            plainText = string.Empty;

            byte[]? key = null;
            try
            {
                key = UnprotectKey();
                return Crypto.TryDecrypt(key, payload, out plainText);
            }
            catch (InvalidOperationException)
            {
                // Vault locked.
                return false;
            }
            finally
            {
                if (key != null)
                    CryptographicOperations.ZeroMemory(key);
            }
        }

        /// <summary>Drops the key, putting the vault back in its locked state.</summary>
        public void Lock()
        {
            lock (_gate)
            {
                ClearProtectedKey();
            }
        }

        private byte[] UnprotectKey()
        {
            byte[] protectedKey;

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (_protectedKey == null)
                    throw new InvalidOperationException("The vault is locked.");

                protectedKey = _protectedKey;
            }

            return ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
        }

        private void ClearProtectedKey()
        {
            if (_protectedKey == null)
                return;

            CryptographicOperations.ZeroMemory(_protectedKey);
            _protectedKey = null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                ClearProtectedKey();
                _disposed = true;
            }
        }
    }
}
