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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.Security;

public static class TokenStore
{
    private const string ApplicationName = "NoMercyMediaServer";
    private const string ProtectorPurpose = "NoMercyMediaServer.TokenProtection";

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

    public static string EncryptToken(string? token)
    {
        if (string.IsNullOrEmpty(value: token))
            return string.Empty;

        return EnsureInitialized().Protect(plaintext: token);
    }

    public static string? DecryptToken(string? token)
    {
        if (string.IsNullOrEmpty(value: token))
            return null;

        try
        {
            return EnsureInitialized().Unprotect(protectedData: token);
        }
        catch (Exception)
        {
            // Return null — caller treats this as "no value" → triggers re-auth.
            // Never return raw ciphertext (leaks internal state, burns API rate limits).
            return null;
        }
    }

    // Bootstraps a Protector when called before the DI container exists (e.g. early
    // boot reads of the Configuration table). The keyring directory and application
    // name match ServiceConfiguration.ConfigureCoreServices so this standalone
    // provider and the DI-built provider share on-disk keys — ciphertext written by
    // either is decryptable by the other.
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
                .SetApplicationName(applicationName: ApplicationName);

            ServiceProvider provider = services.BuildServiceProvider();
            IDataProtectionProvider dataProtectionProvider =
                provider.GetRequiredService<IDataProtectionProvider>();
            _protector = dataProtectionProvider.CreateProtector(purpose: ProtectorPurpose);
            return _protector;
        }
    }
}
