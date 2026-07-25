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
using NoMercyQueue.Core.Interfaces;

namespace NoMercy.MediaProcessing.Intake;

public sealed class IntakeSettings(IConfigurationStore configurationStore) : IIntakeSettings
{
    private const string DropFolderKey = "intake.drop_folder";
    private const string TokenHashKey = "intake.token_hash";

    public Task<string?> GetDropFolderAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string? value = configurationStore.GetValue(DropFolderKey);
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }

    public async Task SetDropFolderAsync(string? path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await configurationStore.SetValueAsync(DropFolderKey, path ?? string.Empty);
    }

    public Task<bool> HasTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(configurationStore.HasKey(TokenHashKey));
    }

    public async Task<string> IssueTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string plaintext = Convert
            .ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        await configurationStore.SetValueAsync(TokenHashKey, HashToken(plaintext));

        return plaintext;
    }

    public Task<bool> VerifyTokenAsync(string? presented, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(presented))
            return Task.FromResult(false);

        string? storedHash = configurationStore.GetValue(TokenHashKey);
        if (string.IsNullOrEmpty(storedHash))
            return Task.FromResult(false);

        byte[] storedHashBytes = Convert.FromHexString(storedHash);
        byte[] presentedHashBytes = Convert.FromHexString(HashToken(presented));

        return Task.FromResult(
            CryptographicOperations.FixedTimeEquals(storedHashBytes, presentedHashBytes)
        );
    }

    private static string HashToken(string plaintext)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexStringLower(hashBytes);
    }
}
