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

namespace NoMercy.Tests.Storage.Faults;

/// <summary>
/// Pins the NFS4ERR_EXPIRED / BAD_SESSION / BAD_STATEID / STALE_CLIENTID
/// recovery contract: when libnfs reports any of these on the first attempt,
/// the driver tears down its context, remounts, and retries the operation
/// once. A second failure surfaces the error to the caller.
///
/// Every test runs the driver against <see cref="FaultyLibNfs"/> — no Docker,
/// no NFS server, no flake.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NfsExpiredRecoveryTests
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

    private static (NfsStorageDriver driver, FaultyLibNfs lib) BuildDriver()
    {
        FaultyLibNfs lib = new();
        lib.SeedDir(path: "/");
        NfsStorageDriver driver = new(config: BuildConfig(), libNfs: lib);
        return (driver, lib);
    }

    [Fact]
    public void OpenRead_recovers_when_first_Open_returns_NFS4ERR_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            byte[] payload = [1, 2, 3, 4, 5];
            lib.Seed(path: "/file.bin", content: payload);

            // First Open returns EXPIRED, second succeeds via remount path.
            // Stat is fine throughout (the first Stat in OpenRead uses lib's
            // call counter, then Open's first call hits the fault).
            lib.Faults[key: "Open:0"] = (-11, "NFS4ERR_EXPIRED(-11)");

            using Stream s = driver.OpenRead(path: "/file.bin");
            byte[] buffer = new byte[payload.Length];
            int read = s.Read(buffer: buffer, offset: 0, count: buffer.Length);

            read.Should().Be(expected: payload.Length);
            buffer.Should().BeEquivalentTo(expectation: payload);

            lib.CallCounts[key: "Open"].Should().Be(expected: 2, because: "first Open=EXPIRED triggers Remount + retry");
            lib.CallCounts[key: "Mount"]
                .Should()
                .BeGreaterThan(expected: 1, because: "Remount calls Mount on the fresh ctx");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_recovers_when_first_Stat_returns_NFS4ERR_BAD_SESSION()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [42]);
            lib.Faults[key: "Stat64:0"] = (-1, "NFS4ERR_BAD_SESSION");

            using Stream s = driver.OpenRead(path: "/file.bin");
            s.Length.Should().Be(expected: 1);

            lib.CallCounts[key: "Stat64"]
                .Should()
                .Be(expected: 2, because: "first Stat=BAD_SESSION triggers Remount + retry");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_recovers_when_BAD_STATEID()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Open:0"] = (-1, "NFS4ERR_BAD_STATEID");

            using Stream s = driver.OpenRead(path: "/file.bin");
            s.Length.Should().Be(expected: 1);

            lib.CallCounts[key: "Open"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_recovers_when_STALE_CLIENTID()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Open:0"] = (-1, "NFS4ERR_STALE_CLIENTID");

            using Stream s = driver.OpenRead(path: "/file.bin");
            s.Length.Should().Be(expected: 1);

            lib.CallCounts[key: "Open"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_does_not_remount_for_NOENT()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            // Stat returns NOENT (-2) — that's the "not found" outcome,
            // not a state-expiry. Driver must NOT remount; just throw.
            Action act = () => driver.OpenRead(path: "/missing.bin");
            act.Should().Throw<FileNotFoundException>();

            lib.CallCounts[key: "Stat64"].Should().Be(expected: 1, because: "no remount on plain NOENT");
            // Mount is called once during ctor; no extra Mount from Remount.
            lib.CallCounts[key: "Mount"].Should().Be(expected: 1);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_throws_when_remount_also_fails_with_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            // Both attempts fail. Driver retries exactly once, then surfaces.
            lib.Faults[key: "Open:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Open:1"] = (-1, "NFS4ERR_PERM");

            Action act = () => driver.OpenRead(path: "/file.bin");
            act.Should().Throw<IOException>();

            lib.CallCounts[key: "Open"].Should().Be(expected: 2, because: "exactly one remount-retry, no infinite loop");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenWrite_recovers_when_first_Open_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Faults[key: "Open:0"] = (-11, "NFS4ERR_EXPIRED");

            using Stream s = driver.OpenWrite(path: "/new.bin", overwrite: true);
            byte[] payload = [1, 2, 3];
            s.Write(buffer: payload, offset: 0, count: payload.Length);
            s.Flush();

            lib.CallCounts[key: "Open"].Should().BeGreaterThanOrEqualTo(expected: 2, because: "Remount + retry");
            lib.Files.Should().ContainKey(expected: "/new.bin");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void Remount_runs_full_init_version_uid_mount_sequence()
    {
        FaultyLibNfs lib = new();
        lib.SeedDir(path: "/");
        NfsDriverConfig config = new(
            Server: "test.local",
            Export: "/export",
            Version: 4,
            Uid: 1000,
            Gid: 1000,
            Port: 2049,
            MountPort: null
        );
        NfsStorageDriver driver = new(config: config, libNfs: lib);
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Open:0"] = (-11, "NFS4ERR_EXPIRED");

            using Stream _ = driver.OpenRead(path: "/file.bin");

            // Constructor: 1 InitContext + 1 SetVersion + 1 SetUid + 1 SetGid + 1 Mount
            // Remount: +1 of each (but DestroyContext runs on the old ctx first).
            lib.CallCounts[key: "InitContext"].Should().Be(expected: 2);
            lib.CallCounts[key: "SetVersion"].Should().Be(expected: 2);
            lib.CallCounts[key: "SetUid"].Should().Be(expected: 2);
            lib.CallCounts[key: "SetGid"].Should().Be(expected: 2);
            lib.CallCounts[key: "Mount"].Should().Be(expected: 2);
            lib.CallCounts[key: "DestroyContext"].Should().Be(expected: 1, because: "old ctx destroyed before remount");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void OpenRead_only_retries_once_even_on_repeated_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            // Make every Stat attempt return EXPIRED to prove we don't loop forever.
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Stat64:1"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Stat64:2"] = (-11, "NFS4ERR_EXPIRED");

            Action act = () => driver.OpenRead(path: "/file.bin");
            act.Should().Throw<Exception>();

            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2, because: "exactly one retry attempt, no loop");
        }
        finally
        {
            driver.Dispose();
        }
    }
}
