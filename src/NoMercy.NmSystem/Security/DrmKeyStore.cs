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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.Security;

/// <summary>
/// Protected-at-rest storage for AES-128 HLS DRM keys. Mirrors
/// <see cref="TokenStore"/>'s IDataProtector pattern so a decryption key
/// never sits on disk as plaintext once the encode that generated it exits.
///
/// One file per key under <see cref="AppFiles.DrmKeysPath"/>, named by a
/// SHA-256 hash of the profile's <c>key_uri</c> — the same identifier a
/// future authorized key-serving endpoint would receive from the client's
/// HLS request. No such endpoint exists yet; wiring one is a REST-contract
/// decision for server-api-specialist, plus a durability call for
/// server-database-specialist on whether this belongs in a DB table instead
/// of the filesystem. This store only closes the "raw key shipped next to
/// the ciphertext" leak and keeps the key retrievable once that endpoint exists.
/// </summary>
public static class DrmKeyStore
{
    private const string ProtectorPurpose = "NoMercyMediaServer.DrmKeyProtection";

    private static readonly object InitLock = new();
    private static IDataProtector? _protector;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        lock (InitLock)
        {
            if (_protector is not null)
                return;

            IDataProtectionProvider dataProtectionProvider =
                serviceProvider.GetRequiredService<IDataProtectionProvider>();
            _protector = dataProtectionProvider.CreateProtector(purpose: ProtectorPurpose);
        }
    }

    /// <summary>
    /// Protects and persists a 16-byte key + 16-byte IV pair, keyed by the
    /// key_uri the client will use to fetch it. Overwrites any existing entry
    /// for the same key_uri (profile re-encodes rotate the key).
    /// </summary>
    public static async Task StoreKeyAsync(
        string keyUri,
        byte[] key,
        byte[] iv,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(value: keyUri))
            throw new ArgumentException(message: "keyUri is required", paramName: nameof(keyUri));
        if (key.Length != 16)
            throw new ArgumentException(message: $"key must be 16 bytes, got {key.Length}", paramName: nameof(key));
        if (iv.Length != 16)
            throw new ArgumentException(message: $"iv must be 16 bytes, got {iv.Length}", paramName: nameof(iv));

        Directory.CreateDirectory(path: AppFiles.DrmKeysPath);

        byte[] payload = new byte[32];
        Buffer.BlockCopy(src: key, srcOffset: 0, dst: payload, dstOffset: 0, count: 16);
        Buffer.BlockCopy(src: iv, srcOffset: 0, dst: payload, dstOffset: 16, count: 16);

        string protectedBase64 = Convert.ToBase64String(inArray: EnsureInitialized().Protect(plaintext: payload));
        string path = PathForKeyUri(keyUri: keyUri);
        await File.WriteAllTextAsync(path: path, contents: protectedBase64, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
    }

    /// <summary>
    /// Recovers the key + IV previously stored for <paramref name="keyUri"/>.
    /// Returns null when nothing is stored or the ciphertext can't be
    /// unprotected (keyring rotated/missing) — callers treat that as "no key".
    /// </summary>
    public static async Task<(byte[] Key, byte[] Iv)?> TryGetKeyAsync(
        string keyUri,
        CancellationToken ct
    )
    {
        string path = PathForKeyUri(keyUri: keyUri);
        if (!File.Exists(path: path))
            return null;

        try
        {
            string protectedBase64 = await File.ReadAllTextAsync(path: path, cancellationToken: ct).ConfigureAwait(continueOnCapturedContext: false);
            byte[] payload = EnsureInitialized()
                .Unprotect(protectedData: Convert.FromBase64String(s: protectedBase64));
            return (payload[..16], payload[16..]);
        }
        catch (Exception)
        {
            // Ciphertext unreadable (rotated keyring, corrupt file) — no key.
            return null;
        }
    }

    private static string PathForKeyUri(string keyUri)
    {
        byte[] hash = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: keyUri));
        string id = Convert.ToHexString(inArray: hash).ToLowerInvariant();
        return Path.Combine(path1: AppFiles.DrmKeysPath, path2: $"{id}.key.protected");
    }

    private static IDataProtector EnsureInitialized()
    {
        if (_protector is not null)
            return _protector;

        lock (InitLock)
        {
            if (_protector is not null)
                return _protector;

            if (!Directory.Exists(path: AppFiles.DataProtectionKeysDir))
                Directory.CreateDirectory(path: AppFiles.DataProtectionKeysDir);

            ServiceCollection services = new();
            services
                .AddDataProtection()
                .PersistKeysToFileSystem(directory: new(path: AppFiles.DataProtectionKeysDir))
                .SetApplicationName(applicationName: "NoMercyMediaServer");

            ServiceProvider provider = services.BuildServiceProvider();
            IDataProtectionProvider dataProtectionProvider =
                provider.GetRequiredService<IDataProtectionProvider>();
            _protector = dataProtectionProvider.CreateProtector(purpose: ProtectorPurpose);
            return _protector;
        }
    }
}
