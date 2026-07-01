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

using System.Diagnostics;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Tests.Storage.Container;
using Xunit.Abstractions;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Measures raw streaming throughput (write then read) for every remote driver
/// against the all-in-one container, and prints MB/s. Its job is to answer one
/// question: does a driver keep pace with the IO, or does it add its own ceiling
/// (whole-file buffering, per-byte round-trips, sync stalls)?
///
/// <para><b>What a loopback number means.</b> The container runs over loopback,
/// so these figures measure the <i>driver's own overhead</i> — chunking, native
/// round-trips, allocations — not a real NAS's wire speed. A high loopback number
/// proves the driver is not the bottleneck; the wire is. A low one is a driver
/// bug. The test asserts only a sanity <i>floor</i> that a regression to whole-file
/// buffering or per-byte round-trips would break; the real signal is the logged
/// MB/s, read from the test output.</para>
///
/// <para>Tagged <c>Category=Benchmark</c> so it is filtered out of routine runs
/// (<c>--filter "Category!=Benchmark"</c>) and executed on demand:
/// <c>dotnet test --filter "Category=Benchmark"</c>. Payload size is overridable
/// with the <c>NM_BENCH_MB</c> environment variable (default 256 MiB).</para>
/// </summary>
[Collection("StorageBackends")]
[Trait("Category", "Integration")]
[Trait("Category", "Benchmark")]
public sealed class StorageThroughputBenchmark(StorageBackendsFixture fix, ITestOutputHelper output)
{
    // 256 MiB by default: comfortably past every driver's internal chunk and the
    // ~2 GiB single-array limit a whole-file buffer would hit, while finishing in
    // seconds over loopback. Override with NM_BENCH_MB for a heavier soak.
    private static int PayloadBytes =>
        (int.TryParse(Environment.GetEnvironmentVariable("NM_BENCH_MB"), out int mb) ? mb : 256)
        * 1024
        * 1024;

    // Loopback floor. A working streaming driver clears this by a wide margin;
    // only a hard regression (whole-file buffer thrash, per-byte round-trips, a
    // stall) drops beneath it. Deliberately low so a slow CI host doesn't flake —
    // the number in the log is the signal, this bound just fails an obvious break.
    private const double MinMBytesPerSecond = 8.0;

    private const int BlockSize = 1024 * 1024;

    [SkippableFact]
    public void Smb_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        using SmbStorageDriver driver = fix.BuildSmbDriver();
        Measure("SMB", driver, $"bench/smb-{Ulid.NewUlid()}.bin");
    }

    [SkippableFact]
    public void Nfs_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        Skip.If(!fix.NfsMountable, fix.NfsUnavailableReason ?? "NFS export not mountable");
        using NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(driver is null, "libnfs native library not installed");
        Measure("NFS", driver!, $"/bench-nfs-{Ulid.NewUlid()}.bin");
    }

    [SkippableFact]
    public void S3_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        using S3StorageDriver driver = fix.BuildS3Driver();
        Measure("S3", driver, $"bench/s3-{Ulid.NewUlid()}.bin");
    }

    [SkippableFact]
    public void WebDav_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        Measure("WebDAV", driver, $"bench/webdav-{Ulid.NewUlid()}.bin");
    }

    // Streams the payload in one direction, timing only the transfer. The test
    // never holds the whole payload in memory — it feeds a single reused block on
    // write and consumes into a single reused block on read — so what is timed is
    // the driver moving bytes, not the test allocating them.
    private void Measure(string label, IStorageDriver driver, string path)
    {
        int payload = PayloadBytes;
        byte[] block = new byte[BlockSize];
        FillDeterministic(block);

        double writeMBps = TimeTransfer(
            payload,
            () =>
            {
                Stream w = driver.OpenWrite(path, overwrite: true);
                int written = 0;
                while (written < payload)
                {
                    int len = Math.Min(BlockSize, payload - written);
                    w.Write(block, 0, len);
                    written += len;
                }
                return w; // TimeTransfer disposes it inside the clock (see below)
            }
        );

        double readMBps = TimeTransfer(
            payload,
            () =>
            {
                Stream r = driver.OpenRead(path);
                long total = 0;
                int read;
                while ((read = r.Read(block, 0, BlockSize)) > 0)
                    total += read;
                if (total != payload)
                    throw new IOException($"{label}: read {total} of {payload} bytes");
                return r;
            }
        );

        long actualSize = driver.GetFileSize(path);
        if (actualSize != payload)
            throw new IOException($"{label}: stored size {actualSize} != written {payload}");

        driver.DeleteFile(path);

        output.WriteLine(
            $"{label, -7} {payload / (1024 * 1024)} MiB  "
                + $"write {writeMBps, 7:F1} MB/s   read {readMBps, 7:F1} MB/s"
        );

        writeMBps
            .Should()
            .BeGreaterThan(
                MinMBytesPerSecond,
                $"{label} write throughput regressed (whole-file buffer or per-byte round-trips?)"
            );
        readMBps
            .Should()
            .BeGreaterThan(
                MinMBytesPerSecond,
                $"{label} read throughput regressed (whole-file buffer or per-byte round-trips?)"
            );
    }

    // Runs the transfer, disposing the stream inside the timed region because a
    // write driver's real cost (S3 CompleteMultipartUpload, WebDAV's PUT-on-close)
    // lands on Dispose — timing must include it or the number is a lie.
    private static double TimeTransfer(int payload, Func<Stream> transfer)
    {
        Stopwatch sw = Stopwatch.StartNew();
        Stream stream = transfer();
        stream.Dispose();
        sw.Stop();

        double seconds = sw.Elapsed.TotalSeconds;
        double megabytes = payload / 1_000_000.0;
        return seconds > 0 ? megabytes / seconds : double.PositiveInfinity;
    }

    private static void FillDeterministic(byte[] block)
    {
        unchecked
        {
            uint state = 2166136261u;
            for (int index = 0; index < block.Length; index++)
            {
                state = state * 16777619u + 1u;
                block[index] = (byte)(state >> 24);
            }
        }
    }
}
