using MultiLaunch.DbContexts;
using System.Security.Cryptography;
using System.Text;

namespace MultiLaunch.Services
{
    public class DatabaseService
    {
        private SQLiteDbContext _dbContext;
        private string _key;

        public DatabaseService(SQLiteDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public bool IsPasswordCorrect(string password)
        {
            return true;
        }

        #region Private Functions

        private string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_key);
                aes.IV = new byte[16]; // IV de 16 octets (rempli de zéros, mais idéalement stocké séparément)

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }

        private string Decrypt(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(_key);
                aes.IV = new byte[16]; // Même IV que celui utilisé pour chiffrer

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                    byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
        }


        #endregion
    }
}
