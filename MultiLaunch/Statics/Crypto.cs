using DeviceId;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Management;
using System.Security;

namespace MultiLaunch.Statics
{
    internal static class Crypto
    {
        private static string GetDeviceId()
        {
            using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                return obj["SerialNumber"].ToString();
            }

            throw new InvalidOperationException("Impossible de récupérer l'UUID WMI.");
        }

        private static byte[] GetIV()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(GetDeviceId());
            return bytes.Length >= 16 ? bytes[..16] : bytes;
        }

        public static string Encrypt(string key, string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));

                    aes.Key = keyBytes;
                    //aes.IV = new byte[16]; // IV de 16 octets (rempli de zéros, mais idéalement stocké séparément)
                    aes.IV = GetIV(); // IV de 16 octets (rempli de zéros, mais idéalement stocké séparément)

                    using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                        return Convert.ToBase64String(encryptedBytes);
                    }
                }
            }
        }

        public static string Decrypt(string key, string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                using (var sha256 = SHA256.Create())
                {
                    byte[] keyBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
                    aes.Key = keyBytes;
                    //aes.IV = new byte[16]; // Même IV que celui utilisé pour chiffrer
                    aes.IV = GetIV(); // Même IV que celui utilisé pour chiffrer


                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                        try
                        {
                            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                            return Encoding.UTF8.GetString(decryptedBytes);
                        }
                        catch (CryptographicException ex)
                        {
                            Debug.WriteLine(ex.Message);
                            // Handle decryption failure
                            return string.Empty;
                        }
                        catch (FormatException ex)
                        {
                            Debug.WriteLine(ex.Message);
                            // Handle format exception
                            return string.Empty;
                        }
                        catch (ArgumentNullException ex)
                        {
                            Debug.WriteLine(ex.Message);
                            // Handle null argument exception
                            return string.Empty;
                        }
                        catch (InvalidOperationException ex)
                        {
                            Debug.WriteLine(ex.Message);
                            // Handle invalid operation exception
                            return string.Empty;

                        }
                    }
                }

            }
        }

        public static SecureString ConvertToSecureString(string password)
        {
            if (string.IsNullOrEmpty(password))
                return null;
            SecureString securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            securePassword.MakeReadOnly();
            return securePassword;
        }
    }
}
