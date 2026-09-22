using Microsoft.EntityFrameworkCore;
using MultiLaunch.DbContexts;
using MultiLaunch.Models;
using MultiLaunch.Statics;
using System.Security.Cryptography;

namespace MultiLaunch.Services
{
    public enum VaultState
    {
        /// <summary>No main password has ever been defined.</summary>
        Uninitialized,

        /// <summary>The vault uses the current format and is waiting for the main password.</summary>
        Ready,

        /// <summary>The vault still uses the legacy format and will be migrated on the next successful unlock.</summary>
        Legacy
    }

    /// <summary>
    /// Owns the vault metadata stored in the Settings table and the unlock workflow.
    ///
    /// The main password is verified by decrypting a verification token with a key derived from it:
    /// an attacker no longer gets a known plaintext (previously the Windows account name) to test
    /// candidate passwords against, and each guess now costs a full PBKDF2 derivation.
    /// </summary>
    public class VaultService
    {
        private const string SettingVersion = "vault.version";
        private const string SettingSalt = "vault.salt";
        private const string SettingIterations = "vault.iterations";
        private const string SettingVerifier = "vault.verifier";

        /// <summary>Legacy setting, kept so existing databases can be recognised and migrated.</summary>
        private const string SettingLegacyValidator = "validator";

        private const string CurrentVersion = "2";
        private const string VerifierToken = "MultiLaunch.vault.unlocked";

        /// <summary>Minimum length accepted for a new main password.</summary>
        public const int MinimumPasswordLength = 8;

        private readonly SQLiteDbContext _dbContext;
        private readonly MainPasswordService _mainPasswordService;

        public VaultService(SQLiteDbContext dbContext, MainPasswordService mainPasswordService)
        {
            _dbContext = dbContext;
            _mainPasswordService = mainPasswordService;
        }

        public async Task<VaultState> GetStateAsync()
        {
            string? version = await GetSettingValueAsync(SettingVersion);
            string? verifier = await GetSettingValueAsync(SettingVerifier);

            if (version == CurrentVersion && !string.IsNullOrEmpty(verifier))
                return VaultState.Ready;

            string? legacyValidator = await GetSettingValueAsync(SettingLegacyValidator);

            // An empty validator means "never configured". A validator equal to the Windows account
            // name is the marker left by an interrupted first run; both lead back to setup.
            if (string.IsNullOrEmpty(legacyValidator) || legacyValidator == CurrentWindowsIdentity)
                return VaultState.Uninitialized;

            return VaultState.Legacy;
        }

        /// <summary>Creates a brand new vault and unlocks it.</summary>
        public async Task CreateAsync(string password)
        {
            EnsurePasswordPolicy(password, nameof(password));

            await ApplyNewPasswordAsync(password);
        }

        /// <summary>
        /// Forgotten main password. The stored secrets are unrecoverable without it, so they are
        /// dropped and the vault is rebuilt around a new password.
        ///
        /// Nothing else is touched: the applications, their arguments, and the usernames and
        /// domains of their credentials all survive. Only the encrypted passwords - which no one
        /// can read any more anyway - are cleared, and have to be typed in again.
        /// </summary>
        /// <returns>The number of secrets that were cleared.</returns>
        public async Task<int> ResetAsync(string newPassword)
        {
            EnsurePasswordPolicy(newPassword, nameof(newPassword));

            int clearedSecrets = 0;

            foreach (AppCred credential in await _dbContext.Credentials.ToListAsync())
            {
                if (string.IsNullOrEmpty(credential.Password))
                    continue;

                credential.Password = string.Empty;
                clearedSecrets++;
            }

            // The credential changes are persisted by the same SaveChanges as the new vault
            // metadata: either both land, or neither does.
            await ApplyNewPasswordAsync(newPassword);

            return clearedSecrets;
        }

        /// <summary>
        /// Verifies the main password and, on success, unlocks the session. Legacy vaults are
        /// transparently re-encrypted with the current format.
        /// </summary>
        public async Task<bool> TryUnlockAsync(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            VaultState state = await GetStateAsync();

            return state switch
            {
                VaultState.Ready => await TryUnlockCurrentAsync(password),
                VaultState.Legacy => await TryUnlockLegacyAsync(password),
                _ => false
            };
        }

        /// <summary>Re-keys the vault with a new main password. Requires an unlocked session.</summary>
        public async Task ChangePasswordAsync(string newPassword)
        {
            if (!_mainPasswordService.IsUnlocked)
                throw new InvalidOperationException("The vault must be unlocked to change the main password.");

            EnsurePasswordPolicy(newPassword, nameof(newPassword));

            // Decrypt every secret with the current key before re-keying.
            List<AppCred> credentials = await _dbContext.Credentials.ToListAsync();
            Dictionary<int, string> plainSecrets = new();

            foreach (AppCred credential in credentials)
            {
                if (string.IsNullOrEmpty(credential.Password))
                    continue;

                if (!_mainPasswordService.TryDecrypt(credential.Password, out string plain))
                    throw new CryptographicException($"The stored secret of credential #{credential.Id} could not be read.");

                plainSecrets[credential.Id] = plain;
            }

            await ApplyNewPasswordAsync(newPassword, dataKey =>
            {
                foreach (AppCred credential in credentials)
                {
                    if (plainSecrets.TryGetValue(credential.Id, out string? plain))
                        credential.Password = Crypto.Encrypt(dataKey, plain);
                }
            });
        }

        private async Task<bool> TryUnlockCurrentAsync(string password)
        {
            string? saltText = await GetSettingValueAsync(SettingSalt);
            string? verifier = await GetSettingValueAsync(SettingVerifier);

            if (string.IsNullOrEmpty(saltText) || string.IsNullOrEmpty(verifier))
                return false;

            byte[] salt;
            try
            {
                salt = Convert.FromBase64String(saltText);
            }
            catch (FormatException)
            {
                return false;
            }

            if (!int.TryParse(await GetSettingValueAsync(SettingIterations), out int iterations) || iterations < 1)
                iterations = Crypto.DefaultIterations;

            byte[] masterKey = Crypto.DeriveMasterKey(password, salt, iterations);
            byte[] dataKey = Crypto.DeriveDataKey(masterKey);
            byte[] verificationKey = Crypto.DeriveVerificationKey(masterKey);

            try
            {
                // A wrong password fails the GCM tag check: no oracle, no partial result.
                if (!Crypto.TryDecrypt(verificationKey, verifier, out string token) || !Crypto.FixedTimeEquals(token, VerifierToken))
                    return false;

                _mainPasswordService.SetDataKey(dataKey);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
                CryptographicOperations.ZeroMemory(dataKey);
                CryptographicOperations.ZeroMemory(verificationKey);
            }
        }

        private async Task<bool> TryUnlockLegacyAsync(string password)
        {
            string? legacyValidator = await GetSettingValueAsync(SettingLegacyValidator);

            string? decryptedValidator = Crypto.LegacyDecrypt(password, legacyValidator);
            if (!Crypto.FixedTimeEquals(decryptedValidator, CurrentWindowsIdentity))
                return false;

            await MigrateLegacyAsync(password);
            return true;
        }

        /// <summary>
        /// Re-encrypts every stored secret with the current format, then replaces the legacy
        /// validator with a proper verification token.
        /// </summary>
        private async Task MigrateLegacyAsync(string password)
        {
            List<AppCred> credentials = await _dbContext.Credentials.ToListAsync();

            await ApplyNewPasswordAsync(password, dataKey =>
            {
                foreach (AppCred credential in credentials)
                {
                    if (string.IsNullOrEmpty(credential.Password))
                        continue;

                    string? plain = Crypto.LegacyDecrypt(password, credential.Password);

                    // A secret we cannot read is left untouched rather than destroyed; it will simply
                    // have to be entered again from the Credentials page.
                    if (plain == null)
                        continue;

                    credential.Password = Crypto.Encrypt(dataKey, plain);
                }
            });
        }

        /// <summary>
        /// Derives a fresh set of keys from <paramref name="password"/>, writes the vault metadata
        /// and unlocks the session. <paramref name="rewriteSecrets"/> runs with the new data key,
        /// before anything is persisted, so the secrets and the metadata are saved together.
        /// </summary>
        private async Task ApplyNewPasswordAsync(string password, Action<byte[]>? rewriteSecrets = null)
        {
            byte[] salt = Crypto.CreateSalt();
            int iterations = Crypto.DefaultIterations;

            byte[] masterKey = Crypto.DeriveMasterKey(password, salt, iterations);
            byte[] dataKey = Crypto.DeriveDataKey(masterKey);
            byte[] verificationKey = Crypto.DeriveVerificationKey(masterKey);

            try
            {
                rewriteSecrets?.Invoke(dataKey);

                await SetSettingValueAsync(SettingSalt, Convert.ToBase64String(salt));
                await SetSettingValueAsync(SettingIterations, iterations.ToString());
                await SetSettingValueAsync(SettingVerifier, Crypto.Encrypt(verificationKey, VerifierToken));
                await SetSettingValueAsync(SettingVersion, CurrentVersion);
                await SetSettingValueAsync(SettingLegacyValidator, string.Empty);

                await _dbContext.SaveChangesAsync();

                _mainPasswordService.SetDataKey(dataKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(masterKey);
                CryptographicOperations.ZeroMemory(dataKey);
                CryptographicOperations.ZeroMemory(verificationKey);
            }
        }

        private static void EnsurePasswordPolicy(string password, string parameterName)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
                throw new ArgumentException($"The main password must be at least {MinimumPasswordLength} characters long.", parameterName);
        }

        private static string CurrentWindowsIdentity => System.Security.Principal.WindowsIdentity.GetCurrent().Name;

        private async Task<string?> GetSettingValueAsync(string key)
            => (await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == key))?.Value;

        private async Task SetSettingValueAsync(string key, string value)
        {
            Setting? setting = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == key);

            if (setting == null)
            {
                _dbContext.Settings.Add(new Setting { Key = key, Value = value });
                return;
            }

            setting.Value = value;
        }
    }
}
