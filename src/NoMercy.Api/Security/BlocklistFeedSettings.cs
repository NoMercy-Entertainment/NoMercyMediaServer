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
using NoMercy.NmSystem.Security;
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.Api.Security;

// Encrypted rather than hashed, unlike the intake token: the whole point of this
// one is that the owner can read the URL back out of the dashboard whenever the
// firewall needs re-pointing. A hash would force a rotation every time.
public class BlocklistFeedSettings(IConfigurationStore configurationStore) : IBlocklistFeedSettings
{
    private const string TokenKey = "security.blocklist.token";

    public async Task<string> EnsureTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string? existing = TokenStore.DecryptToken(configurationStore.GetValue(TokenKey));
        if (!string.IsNullOrEmpty(existing))
            return existing;

        return await RotateTokenAsync(ct);
    }

    public async Task<string> RotateTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string plaintext = Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        await configurationStore.SetValueAsync(TokenKey, TokenStore.EncryptToken(plaintext));

        return plaintext;
    }

    public Task<bool> VerifyAsync(string? presented, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(presented))
            return Task.FromResult(false);

        string? stored = TokenStore.DecryptToken(configurationStore.GetValue(TokenKey));
        if (string.IsNullOrEmpty(stored))
            return Task.FromResult(false);

        return Task.FromResult(
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored),
                Encoding.UTF8.GetBytes(presented)
            )
        );
    }
}
