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
[Trait("Category", "Unit")]
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
        lib.SeedDir("/");
        NfsStorageDriver driver = new(BuildConfig(), lib);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.FileExists("/file.bin").Should().BeTrue();
            lib.CallCounts["Stat64"].Should().Be(2);
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
            driver.FileExists("/missing.bin").Should().BeFalse();
            lib.CallCounts["Stat64"].Should().Be(1, "no remount on plain NOENT");
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
            lib.SeedDir("/sub");
            lib.Faults["Stat64:0"] = (-1, "NFS4ERR_BAD_SESSION");

            driver.DirectoryExists("/sub").Should().BeTrue();
            lib.CallCounts["Stat64"].Should().Be(2);
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
            lib.Seed("/file.bin", new byte[100]);
            lib.Faults["Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.GetFileSize("/file.bin").Should().Be(100);
            lib.CallCounts["Stat64"].Should().Be(2);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetLastWriteTimeUtc("/file.bin");
            lib.CallCounts["Stat64"].Should().Be(2);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetCreationTimeUtc("/file.bin");
            lib.CallCounts["Stat64"].Should().Be(2);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Stat64:0"] = (-11, "NFS4ERR_EXPIRED");

            DateTime _ = driver.GetLastAccessTimeUtc("/file.bin");
            lib.CallCounts["Stat64"].Should().Be(2);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Unlink:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.DeleteFile("/file.bin");

            lib.CallCounts["Unlink"].Should().Be(2);
            lib.Files.Should().NotContainKey("/file.bin");
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
            lib.Seed("/old.bin", [1, 2, 3]);
            lib.Faults["Rename:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.MoveFile("/old.bin", "/new.bin");

            lib.CallCounts["Rename"].Should().Be(2);
            lib.Files.Should().NotContainKey("/old.bin").And.ContainKey("/new.bin");
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
            lib.SeedDir("/subdir");
            lib.Faults["RmDir:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.DeleteDirectory("/subdir", recursive: false);

            lib.CallCounts["RmDir"].Should().Be(2);
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
            lib.Faults["MkDir:0"] = (-11, "NFS4ERR_EXPIRED");

            driver.CreateDirectory("/new_dir");

            lib.CallCounts["MkDir"].Should().Be(2);
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
            lib.SeedDir("/sub");
            lib.Faults["OpenDir:0"] = (-11, "NFS4ERR_EXPIRED");

            // ReadDir returns IntPtr.Zero in the fake (empty dir), so the result
            // list will be empty — but the important thing is OpenDir was retried.
            driver.ListDirectories("/sub");

            lib.CallCounts["OpenDir"].Should().Be(2);
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
            lib.Seed("/file.bin", [1]);
            lib.Faults["Unlink:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults["Unlink:1"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults["Unlink:2"] = (-11, "NFS4ERR_EXPIRED");

            Action act = () => driver.DeleteFile("/file.bin");
            act.Should().Throw<IOException>();

            lib.CallCounts["Unlink"].Should().Be(2);
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
            lib.Seed("/old.bin", [1]);
            lib.Faults["Rename:0"] = (-11, "NFS4ERR_EXPIRED");
            lib.Faults["Rename:1"] = (-11, "NFS4ERR_EXPIRED");

            Action act = () => driver.MoveFile("/old.bin", "/new.bin");
            act.Should().Throw<IOException>();

            lib.CallCounts["Rename"].Should().Be(2);
        }
        finally
        {
            driver.Dispose();
        }
    }
}
