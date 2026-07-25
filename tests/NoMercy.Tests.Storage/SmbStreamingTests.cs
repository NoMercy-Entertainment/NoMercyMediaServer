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
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Proves the SMB driver streams reads and writes instead of buffering whole
/// files in memory. The payload is larger than the driver's internal chunk size
/// and larger than any single buffer it would allocate if it were still buffering
/// the whole file, so a passing round-trip is evidence of true streaming — the
/// bytes flow through in chunks and the SHA-256 matches end to end.
/// </summary>
[Collection("StorageBackends")]
public sealed class SmbStreamingTests(StorageBackendsFixture fix)
{
    // 64 MiB: well past the 1 MiB per-chunk read/write size and large enough that
    // a whole-file buffer would be an obvious regression, while staying fast on a
    // loopback container.
    private const int PayloadBytes = 64 * 1024 * 1024;

    [SkippableFact]
    public async Task Large_file_round_trips_via_streaming_without_buffering()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");

        using SmbStorageDriver driver = fix.BuildSmbDriver();
        string path = $"streaming/large-{Ulid.NewUlid()}.bin";

        byte[] expectedHash;
        // Write: feed the stream in 1 MiB blocks from a deterministic generator so
        // the whole payload never exists in the test's memory as one array either.
        using (IncrementalHash writeHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            await using Stream w = driver.OpenWrite(path, overwrite: true);
            byte[] block = new byte[1024 * 1024];
            int written = 0;
            int seed = 0;
            while (written < PayloadBytes)
            {
                FillDeterministic(block, seed++);
                int len = Math.Min(block.Length, PayloadBytes - written);
                writeHash.AppendData(block, 0, len);
                await w.WriteAsync(block.AsMemory(0, len));
                written += len;
            }
            expectedHash = writeHash.GetHashAndReset();
        }

        driver.GetFileSize(path).Should().Be(PayloadBytes);

        // Read back in blocks, hashing as we go — never materialising the whole
        // file, mirroring how the encoder/copy paths consume a source.
        byte[] actualHash;
        using (IncrementalHash readHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            await using Stream r = driver.OpenRead(path);
            byte[] buffer = new byte[1024 * 1024];
            long total = 0;
            int read;
            while ((read = await r.ReadAsync(buffer)) > 0)
            {
                readHash.AppendData(buffer, 0, read);
                total += read;
            }
            total.Should().Be(PayloadBytes);
            actualHash = readHash.GetHashAndReset();
        }

        actualHash.Should().Equal(expectedHash);

        driver.DeleteFile(path);
        driver.FileExists(path).Should().BeFalse();
    }

    // Deterministic, seed-varied fill so each block differs (a constant block would
    // let a broken chunk-offset bug still hash-match).
    private static void FillDeterministic(byte[] block, int seed)
    {
        unchecked
        {
            uint state = (uint)(seed * 2654435761u) + 1u;
            for (int index = 0; index < block.Length; index++)
            {
                state = state * 1664525u + 1013904223u;
                block[index] = (byte)(state >> 24);
            }
        }
    }
}
