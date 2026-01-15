using System.Security.Cryptography;

namespace NoMercy.NmSystem;

public static class Checksum
{
    public static async Task<string> GetAsync(string filePath)
    {
        await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        byte[] hashBytes = await SHA256.HashDataAsync(fileStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
    
    public static string Get(string filePath)
    {
        const int bufferSize = 1024 * 64; // 64 KB, can be increased to 1MB (1024 * 1024)

        using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read);
        using BufferedStream bufferedStream = new(fileStream, bufferSize);
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(bufferedStream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}