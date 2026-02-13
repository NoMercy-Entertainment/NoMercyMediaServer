using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.Setup;

public static class OfflineJwksCache
{
    private static readonly object CacheLock = new();
    private static RsaSecurityKey? _cachedSigningKey;

    public static RsaSecurityKey? CachedSigningKey
    {
        get
        {
            lock (CacheLock)
            {
                return _cachedSigningKey;
            }
        }
        private set
        {
            lock (CacheLock)
            {
                _cachedSigningKey = value;
            }
        }
    }

    public static void CachePublicKey(string publicKeyBase64)
    {
        try
        {
            File.WriteAllText(AppFiles.AuthKeysFile, publicKeyBase64);
            CachedSigningKey = CreateSecurityKeyFromBase64(publicKeyBase64);
            Logger.Auth("Cached auth public key for offline use");
        }
        catch (Exception e)
        {
            Logger.Auth($"Failed to cache auth public key: {e.Message}", LogEventLevel.Warning);
        }
    }

    public static bool LoadCachedPublicKey()
    {
        try
        {
            if (!File.Exists(AppFiles.AuthKeysFile))
                return false;

            string publicKeyBase64 = File.ReadAllText(AppFiles.AuthKeysFile).Trim();
            if (string.IsNullOrEmpty(publicKeyBase64))
                return false;

            CachedSigningKey = CreateSecurityKeyFromBase64(publicKeyBase64);
            Logger.Auth("Loaded cached auth public key for offline validation");
            return true;
        }
        catch (Exception e)
        {
            Logger.Auth($"Failed to load cached auth public key: {e.Message}", LogEventLevel.Warning);
            return false;
        }
    }

    internal static RsaSecurityKey CreateSecurityKeyFromBase64(string publicKeyBase64)
    {
        byte[] keyBytes = Convert.FromBase64String(publicKeyBase64);
        RSA rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        return new RsaSecurityKey(rsa);
    }
}
