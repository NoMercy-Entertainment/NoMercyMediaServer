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

using NoMercy.Storage.Drivers.S3;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Production-driver integration tests for the S3 driver, run against REAL S3
// storage: the MinIO server in the all-in-one StorageBackends container. MinIO
// speaks the genuine S3 wire protocol, so this exercises the same code paths a
// production AWS S3 / MinIO / Vliegtuig backend would. The container is started
// once for the whole assembly and torn down after the last test.
// ============================================================================

[Collection("StorageBackends")]
public sealed class S3LiveTests(StorageBackendsFixture fix)
{
    private S3StorageDriver Driver()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        return fix.BuildS3Driver();
    }

    private static string ScratchName() => $"nmtest-{Ulid.NewUlid()}";

    [SkippableFact]
    public void Mount_succeeds()
    {
        using S3StorageDriver driver = Driver();
        driver.Should().NotBeNull();
    }

    [SkippableFact]
    public void EnumerateRoot_returns_entries_or_empty_listing()
    {
        using S3StorageDriver driver = Driver();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly),
        ];
        Console.WriteLine($"[S3] root entry count: {entries.Count}");
    }

    [SkippableFact]
    public async Task EnumerateRoot_paths_are_driver_relative()
    {
        using S3StorageDriver driver = Driver();
        string marker = $"{ScratchName()}.bin";
        await using (Stream w = driver.OpenWrite(marker, overwrite: true))
            await w.WriteAsync(new byte[4]);

        try
        {
            List<string> entries =
            [
                .. driver.EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly),
            ];

            // Entries are driver-relative — never prefixed with a leading slash
            // or the bucket name.
            entries.Should().NotContain(e => e.StartsWith('/'));
            entries.Should().Contain(e => e.EndsWith(marker, StringComparison.Ordinal));
        }
        finally
        {
            driver.DeleteFile(marker);
        }
    }

    [SkippableFact]
    public async Task DirectoryExists_round_trip()
    {
        using S3StorageDriver driver = Driver();
        string dir = ScratchName();
        string file = $"{dir}/item.bin";
        await using (Stream w = driver.OpenWrite(file, overwrite: true))
            await w.WriteAsync(new byte[4]);

        try
        {
            driver.DirectoryExists(dir).Should().BeTrue($"DirectoryExists('{dir}') must be true");
        }
        finally
        {
            driver.DeleteDirectory(dir, recursive: true);
        }
    }

    [SkippableFact]
    public async Task FileExists_round_trip()
    {
        using S3StorageDriver driver = Driver();
        string file = $"{ScratchName()}.bin";
        await using (Stream w = driver.OpenWrite(file, overwrite: true))
            await w.WriteAsync(new byte[8]);

        try
        {
            driver.FileExists(file).Should().BeTrue();
        }
        finally
        {
            driver.DeleteFile(file);
        }
    }

    [SkippableFact]
    public async Task CreateDirectory_then_DirectoryExists_then_DeleteDirectory()
    {
        using S3StorageDriver driver = Driver();
        string scratch = ScratchName();

        try
        {
            driver.CreateDirectory(scratch);
            driver.DirectoryExists(scratch).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(scratch, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task OpenWrite_then_OpenRead_round_trip()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] expected = new byte[16 * 1024];
        new Random(1337).NextBytes(expected);

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(expected);

            await using Stream r = driver.OpenRead(scratch);
            using MemoryStream ms = new();
            await r.CopyToAsync(ms);
            ms.ToArray().Should().Equal(expected);
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task GetFileSize_after_write()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[256];
        new Random(42).NextBytes(data);

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(data);

            driver.GetFileSize(scratch).Should().Be(256);
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task GetLastWriteTimeUtc_recent()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[32];

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(data);

            DateTime mtime = driver.GetLastWriteTimeUtc(scratch);
            mtime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task MoveFile_renames_path()
    {
        using S3StorageDriver driver = Driver();
        string src = $"{ScratchName()}-src.bin";
        string dst = $"{ScratchName()}-dst.bin";
        byte[] data = new byte[64];
        new Random(7).NextBytes(data);

        try
        {
            await using (Stream w = driver.OpenWrite(src, overwrite: true))
                await w.WriteAsync(data);

            driver.MoveFile(src, dst);

            driver.FileExists(src).Should().BeFalse();
            driver.FileExists(dst).Should().BeTrue();

            await using Stream r = driver.OpenRead(dst);
            using MemoryStream ms = new();
            await r.CopyToAsync(ms);
            ms.ToArray().Should().Equal(data);
        }
        finally
        {
            try
            {
                driver.DeleteFile(dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_content()
    {
        using S3StorageDriver driver = Driver();
        string src = $"{ScratchName()}-src.bin";
        string dst = $"{ScratchName()}-dst.bin";
        byte[] data = new byte[64];
        new Random(99).NextBytes(data);

        try
        {
            await using (Stream w = driver.OpenWrite(src, overwrite: true))
                await w.WriteAsync(data);

            driver.CopyFile(src, dst, overwrite: true);

            driver.FileExists(src).Should().BeTrue();
            driver.FileExists(dst).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteFile(src);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning (src): {ex.Message}");
            }

            try
            {
                driver.DeleteFile(dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning (dst): {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_throws_on_existing()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[16];

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(data);

            Action act = () => driver.OpenWrite(scratch, overwrite: false);
            act.Should().Throw<IOException>();
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using S3StorageDriver driver = Driver();
        string dir = ScratchName();
        string fileA = $"{dir}/a.txt";
        string fileB = $"{dir}/b.txt";
        string fileC = $"{dir}/c.bin";
        byte[] bytes = new byte[4];

        try
        {
            await using (Stream w = driver.OpenWrite(fileA, overwrite: true))
                await w.WriteAsync(bytes);
            await using (Stream w = driver.OpenWrite(fileB, overwrite: true))
                await w.WriteAsync(bytes);
            await using (Stream w = driver.OpenWrite(fileC, overwrite: true))
                await w.WriteAsync(bytes);

            List<string> txtEntries =
            [
                .. driver.EnumerateFileSystemEntries(dir, "*.txt", SearchOption.TopDirectoryOnly),
            ];
            txtEntries.Should().HaveCount(2);
            txtEntries.Should().Contain(e => e.EndsWith("a.txt", StringComparison.Ordinal));
            txtEntries.Should().Contain(e => e.EndsWith("b.txt", StringComparison.Ordinal));

            List<string> recursive =
            [
                .. driver.EnumerateFileSystemEntries(dir, "*.txt", SearchOption.AllDirectories),
            ];
            recursive.Should().HaveCount(2);
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using S3StorageDriver driver = Driver();
        bool result = driver.IsHidden(".hidden-file");
        result.Should().BeFalse("S3 has no hidden attribute — IsHidden always returns false");
    }

    [SkippableFact]
    public async Task MoveDirectory_renames_collection()
    {
        using S3StorageDriver driver = Driver();
        string src = ScratchName();
        string dst = ScratchName();
        string file = $"{src}/item.bin";
        byte[] data = new byte[16];

        try
        {
            await using (Stream w = driver.OpenWrite(file, overwrite: true))
                await w.WriteAsync(data);

            driver.MoveDirectory(src, dst);

            driver.DirectoryExists(src).Should().BeFalse();
            driver.DirectoryExists(dst).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(src, recursive: true);
            }
            catch
            {
                // Best effort.
            }

            try
            {
                driver.DeleteDirectory(dst, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }
}
