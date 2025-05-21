using System;
using System.Runtime.InteropServices;
using System.Security;

namespace MultiLaunch.Services
{
    public class MainPasswordService : IDisposable
    {
        private SecureString _mainPassword;

        public SecureString Get()
        {
            return _mainPassword;
        }

        public string GetAsString()
        {
            if (_mainPassword == null)
                return null;
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(_mainPassword);
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }

        public void Set(string password)
        {
            // Dispose previous password if present
            _mainPassword?.Dispose();

            if (password == null)
            {
                _mainPassword = null;
                return;
            }

            var secure = new SecureString();
            foreach (char c in password)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            _mainPassword = secure;
        }

        public void Dispose()
        {
            _mainPassword?.Dispose();
            _mainPassword = null;
        }
    }
}