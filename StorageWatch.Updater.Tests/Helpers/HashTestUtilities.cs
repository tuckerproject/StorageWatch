using System.Security.Cryptography;
using System.Text;

namespace StorageWatch.Updater.Tests.Helpers;

public static class HashTestUtilities
{
    public static string ComputeSha256(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return ComputeSha256(bytes);
    }

    public static string ComputeSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }
}
