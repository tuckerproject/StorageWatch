/// <summary>
/// Configuration Field Encryption Abstraction
/// 
/// Provides a pluggable interface for encrypting and decrypting sensitive configuration values.
/// Allows future integration with Windows DPAPI, key vaults, or other encryption schemes.
/// </summary>

namespace StorageWatch.Config.Encryption
{
    /// <summary>
    /// Interface for encrypting and decrypting sensitive configuration values.
    /// Implementations can use various encryption schemes (DPAPI, key vaults, etc.)
    /// </summary>
    public interface IConfigurationEncryptor
    {
        /// <summary>
        /// Encrypts a sensitive value.
        /// </summary>
        /// <param name="plainText">The unencrypted value.</param>
        /// <returns>The encrypted value, ready to be stored in configuration.</returns>
        string Encrypt(string plainText);

        /// <summary>
        /// Decrypts a sensitive value.
        /// </summary>
        /// <param name="cipherText">The encrypted value from configuration.</param>
        /// <returns>The decrypted plaintext value.</returns>
        string Decrypt(string cipherText);
    }
}
