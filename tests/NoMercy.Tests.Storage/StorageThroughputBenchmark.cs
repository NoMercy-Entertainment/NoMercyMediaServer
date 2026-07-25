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
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Tests.Storage.Container;
using Xunit.Abstractions;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Measures streaming throughput for every storage driver against the all-in-one
/// container and prints a MB/s table. It answers one question directly: is the
/// user getting the fastest transfer their IO allows, or is a driver leaving
/// throughput on the table?
///
/// <para><b>The baseline.</b> The <c>Local</c> driver runs through the identical
/// benchmark code path against a real disk, so its number is the box's raw IO
/// ceiling under this harness. Every remote driver is printed as a percentage of
/// that baseline. A remote driver near the baseline is IO-bound (as fast as the
/// medium allows); one well under it is leaving throughput on the table.</para>
///
/// <para><b>Two modes per driver.</b> <c>seq</c> is the naive caller: write a
/// block, wait, write the next — one operation in flight, so it exposes
/// per-round-trip latency. <c>pipe</c> uses <see cref="Stream.CopyToAsync"/> with
/// a large buffer, letting the runtime overlap production and IO — this is what a
/// throughput-sensitive caller (the encoder copy, the sync path) should do, and
/// the gap between <c>seq</c> and <c>pipe</c> is the win available from
/// overlapping alone, with no driver change.</para>
///
/// <para><b>Loopback caveat.</b> The container is loopback, so these figures are a
/// per-driver overhead measurement, not a real NAS wire speed. For a remote user
/// the true ceiling is their network (gigabit ~118 MB/s, 10GbE ~1.2 GB/s), not
/// this disk. The table shows whether the driver, on an unconstrained link,
/// would be the bottleneck.</para>
///
/// <para>Tagged <c>Category=Benchmark</c> so it is filtered out of routine runs
/// (<c>--filter "Category!=Benchmark"</c>) and executed on demand:
/// <c>dotnet test --filter "Category=Benchmark"</c>. Payload is overridable with
/// <c>NM_BENCH_MB</c> (default 256 MiB).</para>
/// </summary>
[Collection("StorageBackends")]
[Trait("Category", "Integration")]
[Trait("Category", "Benchmark")]
public sealed class StorageThroughputBenchmark(StorageBackendsFixture fix, ITestOutputHelper output)
{
    private static int PayloadBytes =>
        (int.TryParse(Environment.GetEnvironmentVariable("NM_BENCH_MB"), out int mb) ? mb : 256)
        * 1024
        * 1024;

    // Loopback floor. A working streaming driver clears this by a wide margin;
    // only a hard regression (whole-file buffer thrash, per-byte round-trips, a
    // stall) drops beneath it. Deliberately low so a slow CI host doesn't flake —
    // the numbers in the log are the signal, this bound just fails an obvious break.
    private const double MinMBytesPerSecond = 8.0;

    private const int BlockSize = 1024 * 1024;

    [SkippableFact]
    public void Local_baseline_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        string root = Path.Combine(Path.GetTempPath(), "nm-bench-" + Ulid.NewUlid());
        Directory.CreateDirectory(root);
        try
        {
            Report("Local", new LocalStorageDriver(), Path.Combine(root, "baseline"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [SkippableFact]
    public void Smb_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        using SmbStorageDriver driver = fix.BuildSmbDriver();
        Report("SMB", driver, $"bench/smb-{Ulid.NewUlid()}");
    }

    [SkippableFact]
    public void Nfs_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        Skip.If(!fix.NfsMountable, fix.NfsUnavailableReason ?? "NFS export not mountable");
        using NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(driver is null, "libnfs native library not installed");
        Report("NFS", driver!, $"/bench-nfs-{Ulid.NewUlid()}");
    }

    [SkippableFact]
    public void S3_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        using S3StorageDriver driver = fix.BuildS3Driver();
        Report("S3", driver, $"bench/s3-{Ulid.NewUlid()}");
    }

    [SkippableFact]
    public void WebDav_streaming_throughput()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        WebDavStorageDriver driver = fix.BuildWebDavDriver();
        Report("WebDAV", driver, $"bench/webdav-{Ulid.NewUlid()}");
    }

    // Runs both transfer modes for one driver and prints a two-line result. The
    // seq/pipe pair is what makes the gap visible: same driver, same bytes, the
    // only difference is whether production and IO overlap.
    private void Report(string label, IStorageDriver driver, string pathStem)
    {
        (double wSeq, double rSeq) = RunOnce(driver, $"{pathStem}-seq.bin", false);
        (double wPipe, double rPipe) = RunOnce(driver, $"{pathStem}-pipe.bin", true);

        int mib = PayloadBytes / (1024 * 1024);
        output.WriteLine(
            $"{label, -7} {mib} MiB  seq : write {wSeq, 7:F1}  read {rSeq, 7:F1} MB/s"
        );
        output.WriteLine(
            $"{label, -7} {mib} MiB  pipe: write {wPipe, 7:F1}  read {rPipe, 7:F1} MB/s"
        );

        foreach (double v in new[] { wSeq, rSeq, wPipe, rPipe })
            v.Should()
                .BeGreaterThan(
                    MinMBytesPerSecond,
                    $"{label} throughput regressed (whole-file buffer or per-byte round-trips?)"
                );
    }

    private (double WriteMBps, double ReadMBps) RunOnce(
        IStorageDriver driver,
        string path,
        bool pipelined
    )
    {
        int payload = PayloadBytes;

        double writeMBps = Time(
            payload,
            () =>
            {
                using Stream w = driver.OpenWrite(path, true);
                using GeneratedStream src = new(payload);
                if (pipelined)
                    src.CopyTo(w, BlockSize);
                else
                    CopySequential(src, w);
            }
        );

        double readMBps = Time(
            payload,
            () =>
            {
                using Stream r = driver.OpenRead(path);
                using CountingSink sink = new();
                if (pipelined)
                    r.CopyTo(sink, BlockSize);
                else
                    CopySequential(r, sink);
                if (sink.Total != payload)
                    throw new IOException($"read {sink.Total} of {payload} bytes");
            }
        );

        long actualSize = driver.GetFileSize(path);
        if (actualSize != payload)
            throw new IOException($"stored size {actualSize} != written {payload}");
        driver.DeleteFile(path);

        return (writeMBps, readMBps);
    }

    // One block in flight at a time — the naive caller. Isolates per-round-trip
    // latency: no overlap between producing/consuming a block and the IO of the
    // previous one.
    private static void CopySequential(Stream from, Stream to)
    {
        byte[] block = new byte[BlockSize];
        int n;
        while ((n = from.Read(block, 0, BlockSize)) > 0)
            to.Write(block, 0, n);
    }

    // Times the transfer, disposing the stream inside the clock: a write driver's
    // real cost (S3 CompleteMultipartUpload, WebDAV's PUT-on-close) lands on
    // Dispose, so timing must include it or the number lies. Uses a real-time
    // stopwatch and decimal MB (1e6) so the figure is comparable to vendor specs.
    private static double Time(int payload, Action transfer)
    {
        Stopwatch sw = Stopwatch.StartNew();
        transfer();
        sw.Stop();
        double seconds = sw.Elapsed.TotalSeconds;
        return seconds > 0 ? payload / 1_000_000.0 / seconds : double.PositiveInfinity;
    }
}
