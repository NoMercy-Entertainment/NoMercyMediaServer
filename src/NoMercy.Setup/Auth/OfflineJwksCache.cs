// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Serilog.Events;

namespace NoMercy.Setup.Auth;

public static class OfflineJwksCache
{
    // LOCAL-ONLY: OfflineJwksCache is called during DI registration (ConfigureAuth) and
    // AuthManager.InitializeAsync — both run before StorageProvider is initialized.
    private static readonly IStorageDriver Backend = new LocalStorageDriver();
    private static readonly object CacheLock = new();

    public static RsaSecurityKey? CachedSigningKey
    {
        get
        {
            lock (CacheLock)
            {
                return field;
            }
        }
        private set
        {
            lock (CacheLock)
            {
                field = value;
            }
        }
    }

    public static void CachePublicKey(string publicKeyBase64)
    {
        try
        {
            using Stream stream = Backend.OpenWrite(path: AppFiles.AuthKeysFile, overwrite: true);
            using StreamWriter writer = new(stream: stream, encoding: Encoding.UTF8, leaveOpen: true);
            writer.Write(value: publicKeyBase64);
            CachedSigningKey = CreateSecurityKeyFromBase64(publicKeyBase64: publicKeyBase64);
            Logger.Auth(message: "Cached auth public key for offline use");
        }
        catch (Exception e)
        {
            Logger.Auth(message: $"Failed to cache auth public key: {e.Message}", level: LogEventLevel.Warning);
        }
    }

    public static bool LoadCachedPublicKey()
    {
        try
        {
            if (!Backend.FileExists(path: AppFiles.AuthKeysFile))
                return false;

            string publicKeyBase64;
            using (StreamReader reader = new(stream: Backend.OpenRead(path: AppFiles.AuthKeysFile)))
                publicKeyBase64 = reader.ReadToEnd().Trim();

            if (string.IsNullOrEmpty(value: publicKeyBase64))
                return false;

            CachedSigningKey = CreateSecurityKeyFromBase64(publicKeyBase64: publicKeyBase64);
            Logger.Auth(message: "Loaded cached auth public key for offline validation");
            return true;
        }
        catch (Exception e)
        {
            Logger.Auth(
                message: $"Failed to load cached auth public key: {e.Message}",
                level: LogEventLevel.Warning
            );
            return false;
        }
    }

    internal static RsaSecurityKey CreateSecurityKeyFromBase64(string publicKeyBase64)
    {
        string cleaned = publicKeyBase64
            .Replace(oldValue: "-----BEGIN PUBLIC KEY-----", newValue: "")
            .Replace(oldValue: "-----END PUBLIC KEY-----", newValue: "")
            .Replace(oldValue: "-----BEGIN RSA PUBLIC KEY-----", newValue: "")
            .Replace(oldValue: "-----END RSA PUBLIC KEY-----", newValue: "")
            .Replace(oldValue: "\n", newValue: "")
            .Replace(oldValue: "\r", newValue: "")
            .Trim();

        byte[] keyBytes = Convert.FromBase64String(s: cleaned);
        RSA rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(source: keyBytes, bytesRead: out _);
        return new(rsa: rsa);
    }
}
