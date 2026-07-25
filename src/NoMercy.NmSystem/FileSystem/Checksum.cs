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

namespace NoMercy.NmSystem.FileSystem;

public static class Checksum
{
    public static async Task<string> GetAsync(string filePath)
    {
        await using FileStream fileStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            true
        );
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
