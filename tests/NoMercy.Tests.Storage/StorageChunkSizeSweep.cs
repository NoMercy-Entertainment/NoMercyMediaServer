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
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Tests.Storage.Container;
using Xunit.Abstractions;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Sweeps the per-request transfer size for each driver against the container and
/// prints MB/s per size, so the fastest chunk is picked from measurement rather
/// than guessed. Loopback (a ~10GbE-equivalent internal port), so a driver here is
/// bounded by its own protocol overhead, not a wire — exactly the regime where
/// chunk size, not the network, decides throughput.
///
/// <para>SMB/NFS sweep a single-request read/write size (SMB is clamped by the
/// fork's raised 8 MiB SMB2 client max; NFS by libnfs's negotiated rsize/wsize).
/// S3 sweeps the multipart part size (fewer, larger HTTP PUTs). WebDAV is not
/// swept: it is one PUT of the whole body, with no chunk on the wire to tune.</para>
///
/// <para>Tagged <c>Category=Benchmark</c> so it is off routine runs and executed
/// on demand: <c>dotnet test --filter "Category=Benchmark"</c>. Payload is
/// overridable with <c>NM_BENCH_MB</c> (default 256 MiB).</para>
/// </summary>
[Collection(name: "StorageBackends")]
[Trait(name: "Category", value: "Integration")]
[Trait(name: "Category", value: "Benchmark")]
public sealed class StorageChunkSizeSweep(StorageBackendsFixture fix, ITestOutputHelper output)
{
    private static int PayloadBytes =>
        (int.TryParse(s: Environment.GetEnvironmentVariable(variable: "NM_BENCH_MB"), result: out int mb) ? mb : 256)
        * 1024
        * 1024;

    // NFS single-request read/write sizes (bytes). libnfs clamps to the negotiated
    // rsize/wsize, so the larger sizes only cut managed round-trips.
    public static readonly int[] NfsChunks =
    [
        512 * 1024,
        1024 * 1024,
        2 * 1024 * 1024,
        4 * 1024 * 1024,
        8 * 1024 * 1024,
    ];

    // SMB single-request sizes (bytes). Capped at 1 MiB: the SMB2 client requests
    // 16 credits (16 x 64 KiB = 1 MiB) and a single read/write above that fails
    // with "Not enough credits". Raising it needs a credit-count change in the
    // fork, not just a larger request — measured, so it is a hard ceiling here.
    public static readonly int[] SmbChunks = [256 * 1024, 512 * 1024, 1024 * 1024];

    // Multipart part sizes for S3 (bytes) — all >= the 5 MB S3 minimum.
    public static readonly int[] S3Parts =
    [
        8 * 1024 * 1024,
        16 * 1024 * 1024,
        32 * 1024 * 1024,
        64 * 1024 * 1024,
    ];

    public static TheoryData<int> NfsChunkCases()
    {
        TheoryData<int> data = [];
        foreach (int chunk in NfsChunks)
            data.Add(p: chunk);
        return data;
    }

    public static TheoryData<int> SmbChunkCases()
    {
        TheoryData<int> data = [];
        foreach (int chunk in SmbChunks)
            data.Add(p: chunk);
        return data;
    }

    public static TheoryData<int> S3PartCases()
    {
        TheoryData<int> data = [];
        foreach (int part in S3Parts)
            data.Add(p: part);
        return data;
    }

    [SkippableTheory]
    [MemberData(memberName: nameof(SmbChunkCases))]
    public void Smb_chunk_sweep(int chunk)
    {
        Skip.If(condition: !fix.Available, reason: fix.StartupError ?? "storage container not available");
        using SmbStorageDriver driver = fix.BuildSmbDriver();
        driver.StreamChunkSize = chunk;
        (double w, double r) = Transfer(driver: driver, path: $"sweep/smb-{chunk}-{Ulid.NewUlid()}.bin");
        output.WriteLine(message: $"SMB   chunk {KiB(bytes: chunk), 5}  write {w, 7:F1}  read {r, 7:F1} MB/s");
    }

    [SkippableTheory]
    [MemberData(memberName: nameof(NfsChunkCases))]
    public void Nfs_chunk_sweep(int chunk)
    {
        Skip.If(condition: !fix.Available, reason: fix.StartupError ?? "storage container not available");
        Skip.If(condition: !fix.NfsMountable, reason: fix.NfsUnavailableReason ?? "NFS export not mountable");
        using NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(condition: driver is null, reason: "libnfs native library not installed");
        driver!.StreamChunkSize = chunk;
        (double w, double r) = Transfer(driver: driver, path: $"/sweep-nfs-{chunk}-{Ulid.NewUlid()}.bin");
        output.WriteLine(message: $"NFS   chunk {KiB(bytes: chunk), 5}  write {w, 7:F1}  read {r, 7:F1} MB/s");
    }

    [SkippableTheory]
    [MemberData(memberName: nameof(S3PartCases))]
    public void S3_part_sweep(int part)
    {
        Skip.If(condition: !fix.Available, reason: fix.StartupError ?? "storage container not available");
        using S3StorageDriver driver = fix.BuildS3Driver();
        driver.StreamPartSize = part;
        (double w, double r) = Transfer(driver: driver, path: $"sweep/s3-{part}-{Ulid.NewUlid()}.bin");
        output.WriteLine(message: $"S3    part  {KiB(bytes: part), 5}  write {w, 7:F1}  read {r, 7:F1} MB/s");
    }

    private static (double WriteMBps, double ReadMBps) Transfer(IStorageDriver driver, string path)
    {
        int payload = PayloadBytes;

        double write = Time(
            payload: payload,
            transfer: () =>
            {
                using Stream w = driver.OpenWrite(path: path, overwrite: true);
                using GeneratedStream src = new(length: payload);
                src.CopyTo(destination: w, bufferSize: 1024 * 1024);
            }
        );

        double read = Time(
            payload: payload,
            transfer: () =>
            {
                using Stream r = driver.OpenRead(path: path);
                using CountingSink sink = new();
                r.CopyTo(destination: sink, bufferSize: 1024 * 1024);
                if (sink.Total != payload)
                    throw new IOException(message: $"read {sink.Total} of {payload} bytes");
            }
        );

        if (driver.GetFileSize(path: path) != payload)
            throw new IOException(message: "stored size mismatch");
        driver.DeleteFile(path: path);
        return (write, read);
    }

    private static double Time(int payload, Action transfer)
    {
        Stopwatch sw = Stopwatch.StartNew();
        transfer();
        sw.Stop();
        double seconds = sw.Elapsed.TotalSeconds;
        return seconds > 0 ? payload / 1_000_000.0 / seconds : double.PositiveInfinity;
    }

    private static string KiB(int bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / (1024 * 1024)}M" : $"{bytes / 1024}K";
}
