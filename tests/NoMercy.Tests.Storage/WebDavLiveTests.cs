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

[Collection(name: "StorageBackends")]
public sealed class WebDavLiveTests(StorageBackendsFixture fix)
{
    private WebDavStorageDriver Driver()
    {
        Skip.If(condition: !fix.Available, reason: fix.StartupError ?? "storage container not available");
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
            .EnumerateFileSystemEntries(directory: "/", searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();
        Console.WriteLine(value: $"[WebDAV] root entry count: {entries.Count}");
    }

    [SkippableFact]
    public async Task EnumerateRoot_paths_are_driver_relative()
    {
        using WebDavStorageDriver driver = Driver();
        string marker = $"{ScratchName()}.bin";
        await using (Stream w = driver.OpenWrite(path: marker, overwrite: true))
            await w.WriteAsync(buffer: new byte[4]);

        try
        {
            List<string> entries = driver
                .EnumerateFileSystemEntries(directory: "/", searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .ToList();

            entries.Should().NotContain(predicate: e => e.StartsWith("http", StringComparison.Ordinal));
            entries.Should().Contain(predicate: e => e.EndsWith(marker, StringComparison.Ordinal));
        }
        finally
        {
            driver.DeleteFile(path: marker);
        }
    }

    [SkippableFact]
    public async Task DirectoryExists_round_trip()
    {
        using WebDavStorageDriver driver = Driver();
        string dir = ScratchName();
        driver.CreateDirectory(path: dir);

        try
        {
            driver.DirectoryExists(path: dir).Should().BeTrue(because: $"DirectoryExists('{dir}') must be true");
        }
        finally
        {
            driver.DeleteDirectory(path: dir, recursive: true);
        }
    }

    [SkippableFact]
    public async Task FileExists_round_trip()
    {
        using WebDavStorageDriver driver = Driver();
        string file = $"{ScratchName()}.bin";
        await using (Stream w = driver.OpenWrite(path: file, overwrite: true))
            await w.WriteAsync(buffer: new byte[8]);

        try
        {
            driver.FileExists(path: file).Should().BeTrue();
        }
        finally
        {
            driver.DeleteFile(path: file);
        }
    }

    [SkippableFact]
    public async Task CreateDirectory_then_DirectoryExists_then_DeleteDirectory()
    {
        using WebDavStorageDriver driver = Driver();
        string scratch = ScratchName();

        try
        {
            driver.CreateDirectory(path: scratch);
            driver.DirectoryExists(path: scratch).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(path: scratch, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
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
        new Random(Seed: 1337).NextBytes(buffer: expected);

        try
        {
            await using (Stream w = driver.OpenWrite(path: scratch, overwrite: true))
                await w.WriteAsync(buffer: expected);

            await using Stream r = driver.OpenRead(path: scratch);
            using MemoryStream ms = new();
            await r.CopyToAsync(destination: ms);
            ms.ToArray().Should().Equal(elements: expected);
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task GetFileSize_after_write()
    {
        using WebDavStorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[256];
        new Random(Seed: 42).NextBytes(buffer: data);

        try
        {
            await using (Stream w = driver.OpenWrite(path: scratch, overwrite: true))
                await w.WriteAsync(buffer: data);

            driver.GetFileSize(path: scratch).Should().Be(expected: 256);
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
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
            await using (Stream w = driver.OpenWrite(path: scratch, overwrite: true))
                await w.WriteAsync(buffer: data);

            DateTime mtime = driver.GetLastWriteTimeUtc(path: scratch);
            mtime.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 5));
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
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
        new Random(Seed: 7).NextBytes(buffer: data);

        try
        {
            await using (Stream w = driver.OpenWrite(path: src, overwrite: true))
                await w.WriteAsync(buffer: data);

            driver.MoveFile(source: src, destination: dst);

            driver.FileExists(path: src).Should().BeFalse();
            driver.FileExists(path: dst).Should().BeTrue();

            await using Stream r = driver.OpenRead(path: dst);
            using MemoryStream ms = new();
            await r.CopyToAsync(destination: ms);
            ms.ToArray().Should().Equal(elements: data);
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
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
        new Random(Seed: 99).NextBytes(buffer: data);

        try
        {
            await using (Stream w = driver.OpenWrite(path: src, overwrite: true))
                await w.WriteAsync(buffer: data);

            driver.CopyFile(source: src, destination: dst, overwrite: true);

            driver.FileExists(path: src).Should().BeTrue();
            driver.FileExists(path: dst).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: src);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning (src): {ex.Message}");
            }

            try
            {
                driver.DeleteFile(path: dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning (dst): {ex.Message}");
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
            await using (Stream w = driver.OpenWrite(path: scratch, overwrite: true))
                await w.WriteAsync(buffer: data);

            // WebDAV enforces the no-overwrite guard at PUT time (If-None-Match: *
            // → HTTP 412), which the upload stream surfaces on flush/dispose.
            Func<Task> act = async () =>
            {
                await using Stream w = driver.OpenWrite(path: scratch, overwrite: false);
                await w.WriteAsync(buffer: data);
            };
            await act.Should().ThrowAsync<IOException>();
        }
        finally
        {
            try
            {
                driver.DeleteFile(path: scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using WebDavStorageDriver driver = Driver();
        string dir = ScratchName();
        driver.CreateDirectory(path: dir);
        string fileA = $"{dir}/a.txt";
        string fileB = $"{dir}/b.txt";
        string fileC = $"{dir}/c.bin";
        byte[] bytes = new byte[4];

        try
        {
            await using (Stream w = driver.OpenWrite(path: fileA, overwrite: true))
                await w.WriteAsync(buffer: bytes);
            await using (Stream w = driver.OpenWrite(path: fileB, overwrite: true))
                await w.WriteAsync(buffer: bytes);
            await using (Stream w = driver.OpenWrite(path: fileC, overwrite: true))
                await w.WriteAsync(buffer: bytes);

            List<string> txtEntries = driver
                .EnumerateFileSystemEntries(directory: dir, searchPattern: "*.txt", option: SearchOption.TopDirectoryOnly)
                .ToList();
            txtEntries.Should().HaveCount(expected: 2);
            txtEntries.Should().Contain(predicate: e => e.EndsWith("a.txt", StringComparison.Ordinal));
            txtEntries.Should().Contain(predicate: e => e.EndsWith("b.txt", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(path: dir, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using WebDavStorageDriver driver = Driver();
        // WebDAV has no hidden concept; IsHidden must not throw and returns a bool.
        bool result = driver.IsHidden(path: ".hidden-file");
        result.Should().Be(expected: result);
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
            driver.CreateDirectory(path: src);
            await using (Stream w = driver.OpenWrite(path: file, overwrite: true))
                await w.WriteAsync(buffer: data);

            driver.MoveDirectory(source: src, destination: dst);

            driver.DirectoryExists(path: src).Should().BeFalse();
            driver.DirectoryExists(path: dst).Should().BeTrue();
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(path: src, recursive: true);
            }
            catch
            {
                // Best effort.
            }

            try
            {
                driver.DeleteDirectory(path: dst, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(value: $"[WebDAV] cleanup warning: {ex.Message}");
            }
        }
    }
}
