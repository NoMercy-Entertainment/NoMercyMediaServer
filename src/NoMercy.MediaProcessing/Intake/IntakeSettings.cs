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

        string? value = configurationStore.GetValue(key: DropFolderKey);
        return Task.FromResult(result: string.IsNullOrEmpty(value: value) ? null : value);
    }

    public async Task SetDropFolderAsync(string? path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await configurationStore.SetValueAsync(key: DropFolderKey, value: path ?? string.Empty);
    }

    public Task<bool> HasTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(result: configurationStore.HasKey(key: TokenHashKey));
    }

    public async Task<string> IssueTokenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] tokenBytes = RandomNumberGenerator.GetBytes(count: 32);
        string plaintext = Convert
            .ToBase64String(inArray: tokenBytes)
            .TrimEnd(trimChar: '=')
            .Replace(oldChar: '+', newChar: '-')
            .Replace(oldChar: '/', newChar: '_');

        await configurationStore.SetValueAsync(key: TokenHashKey, value: HashToken(plaintext: plaintext));

        return plaintext;
    }

    public Task<bool> VerifyTokenAsync(string? presented, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(value: presented))
            return Task.FromResult(result: false);

        string? storedHash = configurationStore.GetValue(key: TokenHashKey);
        if (string.IsNullOrEmpty(value: storedHash))
            return Task.FromResult(result: false);

        byte[] storedHashBytes = Convert.FromHexString(s: storedHash);
        byte[] presentedHashBytes = Convert.FromHexString(s: HashToken(plaintext: presented));

        return Task.FromResult(
            result: CryptographicOperations.FixedTimeEquals(left: storedHashBytes, right: presentedHashBytes)
        );
    }

    private static string HashToken(string plaintext)
    {
        byte[] hashBytes = SHA256.HashData(source: Encoding.UTF8.GetBytes(s: plaintext));
        return Convert.ToHexStringLower(inArray: hashBytes);
    }
}
