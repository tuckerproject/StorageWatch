/// <summary>
/// No-Operation Configuration Encryptor
/// 
/// Default implementation that performs no encryption.
/// Used until a real encryption provider (DPAPI, Key Vault) is configured.
/// </summary>

namespace StorageWatch.Config.Encryption
{
    /// <summary>
    /// Default no-op encryptor for configuration values.
    /// Returns plaintext as-is. Replace with a real implementation (DPAPI, etc.) in production.
    /// </summary>
    public class NoOpConfigurationEncryptor : IConfigurationEncryptor
    {
        /// <summary>
        /// Returns the plaintext unchanged (no encryption).
        /// </summary>
        public string Encrypt(string plainText) => plainText;

        /// <summary>
        /// Returns the ciphertext unchanged (no decryption).
        /// </summary>
        public string Decrypt(string cipherText) => cipherText;
    }
}
