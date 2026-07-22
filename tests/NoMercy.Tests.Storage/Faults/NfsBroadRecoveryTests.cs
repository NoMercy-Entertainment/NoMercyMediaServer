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
/// Pins the EXPIRED-recovery contract for every <see cref="NfsStorageDriver"/>
/// entry point that touches state-bearing libnfs operations beyond OpenRead/
/// OpenWrite (which are covered separately in
/// <see cref="NfsExpiredRecoveryTests"/>).
///
/// Each test injects an EXPIRED on the FIRST call to a specific libnfs method,
/// asserts the operation succeeds via remount, and asserts the call count
/// proves exactly one retry happened.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NfsBroadRecoveryTests
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

    // -----------------------------------------------------------------------
    // FileExists / DirectoryExists (Stat-backed, log+return-false on failure)
    // -----------------------------------------------------------------------

    [Fact]
    public void FileExists_recovers_when_first_Stat_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.FileExists(path: "/file.bin").Should().BeTrue();
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void FileExists_returns_false_on_NOENT_without_remount()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            driver.FileExists(path: "/missing.bin").Should().BeFalse();
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 1, because: "no remount on plain NOENT");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void DirectoryExists_recovers_when_first_Stat_returns_BAD_SESSION()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.SeedDir(path: "/sub");
            lib.Faults[key: "Stat64:0"] = (-1, "NFS4ERR_BAD_SESSION");

            driver.DirectoryExists(path: "/sub").Should().BeTrue();
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // GetFileSize / GetLastWriteTimeUtc / GetCreationTimeUtc / GetLastAccessTimeUtc
    // -----------------------------------------------------------------------

    [Fact]
    public void GetFileSize_recovers_when_first_Stat_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: new byte[100]);
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.GetFileSize(path: "/file.bin").Should().Be(expected: 100);
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void GetLastWriteTimeUtc_recovers_when_first_Stat_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetLastWriteTimeUtc(path: "/file.bin");
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void GetCreationTimeUtc_recovers_when_first_Stat_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetCreationTimeUtc(path: "/file.bin");
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void GetLastAccessTimeUtc_recovers_when_first_Stat_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetLastAccessTimeUtc(path: "/file.bin");
            lib.CallCounts[key: "Stat64"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // Mutating ops: Unlink / Rename / RmDir / MkDir
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteFile_recovers_when_first_Unlink_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Unlink:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.DeleteFile(path: "/file.bin");

            lib.CallCounts[key: "Unlink"].Should().Be(expected: 2);
            lib.Files.Should().NotContainKey(unexpected: "/file.bin");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void MoveFile_recovers_when_first_Rename_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/old.bin", content: [1, 2, 3]);
            lib.Faults[key: "Rename:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.MoveFile(source: "/old.bin", destination: "/new.bin");

            lib.CallCounts[key: "Rename"].Should().Be(expected: 2);
            lib.Files.Should().NotContainKey(unexpected: "/old.bin").And.ContainKey(expected: "/new.bin");
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void DeleteDirectory_non_recursive_recovers_when_first_RmDir_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.SeedDir(path: "/subdir");
            lib.Faults[key: "RmDir:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.DeleteDirectory(path: "/subdir", recursive: false);

            lib.CallCounts[key: "RmDir"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void CreateDirectory_recovers_when_first_MkDir_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Faults[key: "MkDir:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.CreateDirectory(path: "/new_dir");

            lib.CallCounts[key: "MkDir"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // Listing: ListDirectories
    // -----------------------------------------------------------------------

    [Fact]
    public void ListDirectories_recovers_when_first_OpenDir_returns_EXPIRED()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.SeedDir(path: "/sub");
            lib.Faults[key: "OpenDir:0"] = (-11, "NFS4ERR_EXPIRED");

            // ReadDir returns IntPtr.Zero in the fake (empty dir), so the result
            // list will be empty — but the important thing is OpenDir was retried.
            driver.ListDirectories(relativePath: "/sub");

            lib.CallCounts[key: "OpenDir"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // No-loop guarantees: every method retries at most once
    // -----------------------------------------------------------------------

    [Fact]
    public void DeleteFile_does_not_loop_when_remount_attempt_also_fails()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/file.bin", content: [1]);
            lib.Faults[key: "Unlink:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Unlink:1"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Unlink:2"] = (-11, "NFS4ERR_EXPIRED");

            Action act = () => driver.DeleteFile(path: "/file.bin");
            act.Should().Throw<IOException>();

            lib.CallCounts[key: "Unlink"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }

    [Fact]
    public void MoveFile_does_not_loop_when_remount_attempt_also_fails()
    {
        (NfsStorageDriver driver, FaultyLibNfs lib) = BuildDriver();
        try
        {
            lib.Seed(path: "/old.bin", content: [1]);
            lib.Faults[key: "Rename:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults[key: "Rename:1"] = (-11, "NFS4ERR_EXPIRED");

            Action act = () => driver.MoveFile(source: "/old.bin", destination: "/new.bin");
            act.Should().Throw<IOException>();

            lib.CallCounts[key: "Rename"].Should().Be(expected: 2);
        }
        finally
        {
            driver.Dispose();
        }
    }
}
