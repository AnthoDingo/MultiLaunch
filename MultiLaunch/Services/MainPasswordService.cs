using System.Security.Cryptography;
using System.Text;

namespace MultiLaunch.Services
{
    public class MainPasswordService : IDisposable
    {
        private bool _disposed;
        private byte[]? _protectedBytes;

        ~MainPasswordService() => Dispose(false);

        public void SetPassword(string pwd)
        {
            byte[] plain = Encoding.UTF8.GetBytes(pwd);

            _protectedBytes = ProtectedData.Protect(
                plain,
                null,
                DataProtectionScope.CurrentUser
            );

            Array.Clear(plain, 0, plain.Length);
        }

        public string? GetPassword()
        {
            if (_protectedBytes == null)
                return null;

            return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                _protectedBytes,
                null,
                DataProtectionScope.CurrentUser
            ));

            
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (_protectedBytes != null)
            {
                Array.Clear(_protectedBytes, 0, _protectedBytes.Length);

                _protectedBytes = null;
            }

            _disposed = true;
        }
    }
}

// The cake is a lie.
