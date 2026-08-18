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

using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Tests.Storage.Faults;

namespace NoMercy.Tests.Storage.Concurrency;

/// <summary>
/// Concurrency contract for <see cref="NfsStorageDriver"/>. The libnfs
/// context is single-threaded; the driver's SemaphoreSlim must serialize
/// every native call against the shared context. <see cref="FaultyLibNfs"/>
/// tracks <see cref="FaultyLibNfs.MaxConcurrentCalls"/> so a broken lock
/// surfaces immediately instead of the random access violations real libnfs
/// produces under contention.
///
/// Each test forces an artificial latency inside the fake so concurrent
/// callers actually pile up — without this, even a missing lock would
/// rarely show because the in-memory operations finish before the next
/// thread starts.
/// </summary>
[Trait("Category", "Unit")]
public class NfsConcurrencyTests
{
    private static NfsDriverConfig BuildConfig() =>
        new(
            Server: "test.local",
            Export: "/export",
            Version: 4,
            Uid: null,
            Gid: null,
            Port: 2049,
            MountPort: null
        );

    private static (NfsStorageDriver driver, FaultyLibNfs lib) BuildDriverWithLatency(
        TimeSpan latency
    )
    {
        FaultyLibNfs lib = new() { ArtificialLatency = latency };
        lib.SeedDir("/");
        NfsStorageDriver driver = new(BuildConfig(), lib);
        return (driver, lib);
    }

    [Fact]
    public void Parallel_FileExists_calls_serialize_through_driver_lock()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriverWithLatency(
            TimeSpan.FromMilliseconds(2)
        );
        try
        {
            for (int i = 0; i < 20; i++)
                lib.Seed($"/file_{i}.bin", [(byte)i]);

            Parallel.For(
                0,
                20,
                new() { MaxDegreeOfParallelism = 8 },
                i => driver.FileExists($"/file_{i}.bin").Should().BeTrue()
            );

            lib.MaxConcurrentCalls.Should()
                .Be(1, "driver _lock must serialize every libnfs call on the shared context");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void Parallel_GetFileSize_calls_serialize_through_driver_lock()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriverWithLatency(
            TimeSpan.FromMilliseconds(2)
        );
        try
        {
            for (int i = 0; i < 20; i++)
                lib.Seed($"/f_{i}.bin", new byte[i + 1]);

            long[] sizes = new long[20];
            Parallel.For(
                0,
                20,
                new() { MaxDegreeOfParallelism = 8 },
                i =>
                {
                    sizes[i] = driver.GetFileSize($"/f_{i}.bin");
                }
            );

            sizes.Should().BeEquivalentTo(Enumerable.Range(1, 20).Select(n => (long)n));
            lib.MaxConcurrentCalls.Should().Be(1);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void Parallel_OpenRead_streams_serialize_libnfs_calls()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriverWithLatency(
            TimeSpan.FromMilliseconds(1)
        );
        try
        {
            byte[] payload = [.. Enumerable.Range(0, 256).Select(n => (byte)n)];
            for (int i = 0; i < 8; i++)
                lib.Seed($"/p_{i}.bin", payload);

            byte[][] reads = new byte[8][];
            Parallel.For(
                0,
                8,
                new() { MaxDegreeOfParallelism = 8 },
                i =>
                {
                    using Stream s = driver.OpenRead($"/p_{i}.bin");
                    byte[] buf = new byte[256];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = s.Read(buf, read, buf.Length - read);
                        if (n == 0)
                            break;
                        read += n;
                    }
                    reads[i] = buf;
                }
            );

            foreach (byte[] r in reads)
                r.Should().BeEquivalentTo(payload);

            lib.MaxConcurrentCalls.Should()
                .Be(1, "shared-context Reads must serialize through _lock");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public async Task Parallel_mixed_read_write_list_operations_do_not_corrupt_each_other()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriverWithLatency(
            TimeSpan.FromMilliseconds(1)
        );
        try
        {
            for (int i = 0; i < 10; i++)
                lib.Seed($"/r_{i}.bin", [(byte)i]);

            int errors = 0;
            Task readTask = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        using Stream s = driver.OpenRead($"/r_{i}.bin");
                        byte[] buf = new byte[1];
                        _ = s.Read(buf, 0, 1);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });
            Task writeTask = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        using Stream s = driver.OpenWrite($"/w_{i}.bin", overwrite: true);
                        s.Write([(byte)i], 0, 1);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });
            Task statTask = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 10; i++)
                        driver.FileExists($"/r_{i}.bin");
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });
            await Task.WhenAll(readTask, writeTask, statTask);

            errors.Should().Be(0, "no operation should fail under mixed concurrency");
            lib.MaxConcurrentCalls.Should().Be(1);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenReadIsolated_stamps_each_context_with_a_unique_client_identity()
    {
        FaultyLibNfs lib = new();
        lib.SeedDir("/");
        byte[] payload = [.. Enumerable.Range(0, 64).Select(n => (byte)n)];
        for (int i = 0; i < 4; i++)
            lib.Seed($"/iso_{i}.bin", payload);

        NfsStorageDriver driver = new(BuildConfig(), lib);
        List<Stream> streams = [];
        try
        {
            for (int i = 0; i < 4; i++)
                streams.Add(driver.OpenReadIsolated($"/iso_{i}.bin"));

            // Every context libnfs stamped — the main context plus one per
            // isolated read — must carry a DISTINCT NFSv4 client name. A shared
            // name folds them into a single open-owner on the server, and their
            // independent local open-seqid counters then collide as
            // NFS4ERR_BAD_SEQID the moment a parallel scan opens several at once.
            lib.ClientNames.Should().HaveCountGreaterThanOrEqualTo(5); // 1 main + 4 isolated
            lib.ClientNames.Values.Distinct()
                .Should()
                .HaveCount(
                    lib.ClientNames.Count,
                    "each isolated read context needs its own NFSv4 client identity"
                );
        }
        finally
        {
            foreach (Stream s in streams)
                s.Dispose();
            driver.Dispose();
        }
    }

    [Fact]
    public void High_contention_stress_holds_lock_invariant()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriverWithLatency(
            TimeSpan.FromMilliseconds(1)
        );
        try
        {
            for (int i = 0; i < 50; i++)
                lib.Seed($"/s_{i}.bin", new byte[10]);

            // 16 worker threads, each doing 50 mixed ops — the kind of load the
            // scanner applies to a vault under a full library refresh.
            Parallel.For(
                0,
                16,
                new() { MaxDegreeOfParallelism = 16 },
                worker =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        int idx = (worker * 50 + i) % 50;
                        switch (i % 4)
                        {
                            case 0:
                                driver.FileExists($"/s_{idx}.bin");
                                break;
                            case 1:
                                driver.GetFileSize($"/s_{idx}.bin");
                                break;
                            case 2:
                                driver.GetLastWriteTimeUtc($"/s_{idx}.bin");
                                break;
                            case 3:
                                using (Stream s = driver.OpenRead($"/s_{idx}.bin"))
                                {
                                    byte[] buf = new byte[10];
                                    _ = s.Read(buf, 0, 10);
                                }
                                break;
                        }
                    }
                }
            );

            lib.MaxConcurrentCalls.Should()
                .Be(
                    1,
                    "even under 16-way contention the driver lock must keep the libnfs context single-threaded"
                );
        }
        finally
        {
            driver.Dispose();
        }
    }
}
