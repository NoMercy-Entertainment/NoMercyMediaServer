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
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: 4096,
            useAsync: true
        );
        byte[] hashBytes = await SHA256.HashDataAsync(source: fileStream);
        return BitConverter.ToString(value: hashBytes).Replace(oldValue: "-", newValue: "").ToLowerInvariant();
    }

    public static string Get(string filePath)
    {
        const int bufferSize = 1024 * 64; // 64 KB, can be increased to 1MB (1024 * 1024)

        using FileStream fileStream = new(path: filePath, mode: FileMode.Open, access: FileAccess.Read);
        using BufferedStream bufferedStream = new(stream: fileStream, bufferSize: bufferSize);
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(inputStream: bufferedStream);
        return BitConverter.ToString(value: hashBytes).Replace(oldValue: "-", newValue: "").ToLowerInvariant();
    }
}
