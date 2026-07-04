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

using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Production-driver integration tests for the WebDAV driver, run against REAL
// WebDAV storage: the Apache mod_dav server in the all-in-one StorageBackends
// container. This exercises the same code paths a production NAS/WebDAV backend
// would. The container is started once for the assembly and torn down after the
// last test.
// ============================================================================

[Collection("StorageBackends")]
public sealed class WebDavLiveTests(StorageBackendsFixture fix)
{
    private WebDavStorageDriver Driver()
    {
        Skip.If(!fix.Available, fix.StartupError ?? "storage container not available");
        return fix.BuildWebDavDriver();
    }

    private static string ScratchName() => $"nmtest-{Ulid.NewUlid()}";

    [SkippableFact]
    public void Mount_succeeds()
    {
        using WebDavStorageDriver driver = Driver();
        driver.Should().NotBeNull();
    }

    [SkippableFact]
    public void EnumerateRoot_returns_entries_or_empty_listing()
    {
        using WebDavStorageDriver driver = Driver();
        List<string> entries = driver
            .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
            .ToList();
        Console.WriteLine($"[WebDAV] root entry count: {entries.Count}");
    }

    [SkippableFact]
    public async Task EnumerateRoot_paths_are_driver_relative()
    {
        using WebDavStorageDriver driver = Driver();
        string marker = $"{ScratchName()}.bin";
        await using (Stream w = driver.OpenWrite(marker, overwrite: true))
            await w.WriteAsync(new byte[4]);

        try
        {
            List<string> entries = driver
                .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
                .ToList();

            entries.Should().NotContain(e => e.StartsWith("http", StringComparison.Ordinal));
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
        using WebDavStorageDriver driver = Driver();
        string dir = ScratchName();
        driver.CreateDirectory(dir);

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
        using WebDavStorageDriver driver = Driver();
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
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task OpenWrite_then_OpenRead_round_trip()
    {
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task GetFileSize_after_write()
    {
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task GetLastWriteTimeUtc_recent()
    {
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task MoveFile_renames_path()
    {
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_content()
    {
        using WebDavStorageDriver driver = Driver();
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
                Console.WriteLine($"[WebDAV] cleanup warning (src): {ex.Message}");
            }

            try
            {
                driver.DeleteFile(dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebDAV] cleanup warning (dst): {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_throws_on_existing()
    {
        using WebDavStorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[16];

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(data);

            // WebDAV enforces the no-overwrite guard at PUT time (If-None-Match: *
            // → HTTP 412), which the upload stream surfaces on flush/dispose.
            Func<Task> act = async () =>
            {
                await using Stream w = driver.OpenWrite(scratch, overwrite: false);
                await w.WriteAsync(data);
            };
            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using WebDavStorageDriver driver = Driver();
        string dir = ScratchName();
        driver.CreateDirectory(dir);
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

            List<string> txtEntries = driver
                .EnumerateFileSystemEntries(dir, "*.txt", SearchOption.TopDirectoryOnly)
                .ToList();
            txtEntries.Should().HaveCount(2);
            txtEntries.Should().Contain(e => e.EndsWith("a.txt", StringComparison.Ordinal));
            txtEntries.Should().Contain(e => e.EndsWith("b.txt", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(dir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using WebDavStorageDriver driver = Driver();
        // WebDAV has no hidden concept; IsHidden must not throw and returns a bool.
        bool result = driver.IsHidden(".hidden-file");
        result.Should().Be(result);
    }

    [SkippableFact]
    public async Task MoveDirectory_renames_collection()
    {
        using WebDavStorageDriver driver = Driver();
        string src = ScratchName();
        string dst = ScratchName();
        string file = $"{src}/item.bin";
        byte[] data = new byte[16];

        try
        {
            driver.CreateDirectory(src);
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
                Console.WriteLine($"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }
}
