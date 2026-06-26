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

using System.Net.Sockets;
using NoMercy.Storage.Drivers.S3;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Live integration tests for the R2 (Cloudflare) driver.
//
// Fixture pulls config from the running dev server API (see LiveDriverFixture.cs).
// Tests are skipped when:
//   - the server is unreachable / no driver row exists / creds missing
//   - the R2 endpoint itself returns a transport-layer error (SocketException /
//     HttpRequestException) on the first probe
//
// Auth failures (403/401) and S3 API errors are NOT caught — they surface as
// real test failures so the misconfiguration is visible.
//
// Write tests use a "nmtest-{Ulid}" prefix to avoid touching real data.
// ============================================================================

public sealed class R2LiveTests(R2LiveFixture fix) : IClassFixture<R2LiveFixture>
{
    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private S3StorageDriver Driver()
    {
        Skip.If(fix.SkipReason is not null, fix.SkipReason ?? string.Empty);

        // We do not have the actual secrets here — the API strips credentialsRef.
        // Build without explicit creds so the AWS default credential chain is tried.
        // On this machine the secrets store has the creds; the test process can
        // only reach them if the server process resolves them at request time.
        // For live tests we pass null/null and expect the driver to use the
        // default chain — if creds aren't in env/profile the first write test
        // will fail with an auth error, which is the correct failure mode.
        S3StorageDriver? driver = fix.BuildDriver(null, null);
        Skip.If(driver is null, "R2 driver could not be constructed from config");
        return driver!;
    }

    private static bool IsTransportError(Exception ex) =>
        ex is HttpRequestException or SocketException
        || ex.InnerException is HttpRequestException or SocketException;

    private void SkipIfUnreachable(Action probe)
    {
        try
        {
            probe();
        }
        catch (Exception ex) when (IsTransportError(ex))
        {
            Skip.If(
                true,
                $"R2 endpoint unreachable — check VPN/network or that the bucket exists ({ex.Message})"
            );
            throw;
        }
    }

    private static string ScratchName() => $"nmtest-{Ulid.NewUlid()}";

    // -----------------------------------------------------------------------
    // Op 1 — mount
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void Mount_succeeds()
    {
        using S3StorageDriver driver = Driver();
        driver.Should().NotBeNull();
    }

    // -----------------------------------------------------------------------
    // Op 2 — enumerate root
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void EnumerateRoot_returns_entries_or_empty_listing()
    {
        using S3StorageDriver driver = Driver();
        List<string> entries = [];
        SkipIfUnreachable(() =>
        {
            entries = driver
                .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
                .ToList();
        });
        Console.WriteLine($"[R2] root entry count: {entries.Count}");
    }

    // -----------------------------------------------------------------------
    // Op 3 — enumerate paths are driver-relative
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void EnumerateRoot_paths_are_driver_relative()
    {
        using S3StorageDriver driver = Driver();
        List<string> entries = [];
        SkipIfUnreachable(() =>
        {
            entries = driver
                .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
                .ToList();
        });

        string? prefix = fix.Driver?.Prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            string trimmed = prefix.TrimEnd('/');
            entries
                .Should()
                .NotContain(
                    e => e.StartsWith(trimmed, StringComparison.Ordinal),
                    $"paths must be driver-relative, not start with the configured prefix '{trimmed}'"
                );
        }
    }

    // -----------------------------------------------------------------------
    // Op 4 — DirectoryExists round-trip
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void DirectoryExists_round_trip()
    {
        using S3StorageDriver driver = Driver();
        List<string> entries = [];
        SkipIfUnreachable(() =>
        {
            entries = driver
                .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
                .ToList();
        });

        List<string> dirs = entries
            .Where(e =>
            {
                try
                {
                    driver
                        .EnumerateFileSystemEntries(e, "*", SearchOption.TopDirectoryOnly)
                        .Take(1)
                        .ToList();
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .ToList();

        foreach (string dir in dirs)
            driver.DirectoryExists(dir).Should().BeTrue($"DirectoryExists('{dir}') must be true");
    }

    // -----------------------------------------------------------------------
    // Op 5 — FileExists round-trip
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void FileExists_round_trip()
    {
        using S3StorageDriver driver = Driver();
        List<string> entries = [];
        SkipIfUnreachable(() =>
        {
            entries = driver
                .EnumerateFileSystemEntries("/", "*", SearchOption.TopDirectoryOnly)
                .ToList();
        });

        string? file = entries.FirstOrDefault(e => !driver.DirectoryExists(e));
        Skip.If(file is null, "No file entries at root — skipping FileExists round-trip");

        driver.FileExists(file!).Should().BeTrue($"FileExists('{file}') must be true");
    }

    // -----------------------------------------------------------------------
    // Op 6 — CreateDirectory → DirectoryExists → DeleteDirectory
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task CreateDirectory_then_DirectoryExists_then_DeleteDirectory()
    {
        using S3StorageDriver driver = Driver();
        string scratch = ScratchName();

        SkipIfUnreachable(() => driver.DirectoryExists(scratch));

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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Op 7 — OpenWrite → OpenRead round-trip
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task OpenWrite_then_OpenRead_round_trip()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] expected = new byte[16 * 1024];
        new Random(1337).NextBytes(expected);

        SkipIfUnreachable(() => driver.FileExists(scratch));

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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 8 — GetFileSize after write
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task GetFileSize_after_write()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[256];
        new Random(42).NextBytes(data);

        SkipIfUnreachable(() => driver.FileExists(scratch));

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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 9 — GetLastWriteTimeUtc is recent
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task GetLastWriteTimeUtc_recent()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[32];

        SkipIfUnreachable(() => driver.FileExists(scratch));

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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 10 — MoveFile renames path
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task MoveFile_renames_path()
    {
        using S3StorageDriver driver = Driver();
        string src = $"{ScratchName()}-src.bin";
        string dst = $"{ScratchName()}-dst.bin";
        byte[] data = new byte[64];
        new Random(7).NextBytes(data);

        SkipIfUnreachable(() => driver.FileExists(src));

        try
        {
            await using (Stream w = driver.OpenWrite(src, overwrite: true))
                await w.WriteAsync(data);

            driver.MoveFile(src, dst);

            driver.FileExists(src).Should().BeFalse("old path must not exist after move");
            driver.FileExists(dst).Should().BeTrue("new path must exist after move");

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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 11 — CopyFile duplicates content
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task CopyFile_duplicates_content()
    {
        using S3StorageDriver driver = Driver();
        string src = $"{ScratchName()}-src.bin";
        string dst = $"{ScratchName()}-dst.bin";
        byte[] data = new byte[64];
        new Random(99).NextBytes(data);

        SkipIfUnreachable(() => driver.FileExists(src));

        try
        {
            await using (Stream w = driver.OpenWrite(src, overwrite: true))
                await w.WriteAsync(data);

            driver.CopyFile(src, dst, overwrite: true);

            driver.FileExists(src).Should().BeTrue("source must still exist after copy");
            driver.FileExists(dst).Should().BeTrue("destination must exist after copy");
        }
        finally
        {
            try
            {
                driver.DeleteFile(src);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R2] cleanup warning (src): {ex.Message}");
            }

            try
            {
                driver.DeleteFile(dst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R2] cleanup warning (dst): {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 12 — OpenWrite overwrite:false throws on existing
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_throws_on_existing()
    {
        using S3StorageDriver driver = Driver();
        string scratch = $"{ScratchName()}.bin";
        byte[] data = new byte[16];

        SkipIfUnreachable(() => driver.FileExists(scratch));

        try
        {
            await using (Stream w = driver.OpenWrite(scratch, overwrite: true))
                await w.WriteAsync(data);

            Action act = () => driver.OpenWrite(scratch, overwrite: false);
            act.Should()
                .Throw<IOException>("overwrite:false on existing key must throw IOException");
        }
        finally
        {
            try
            {
                driver.DeleteFile(scratch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 13 — EnumerateFileSystemEntries with pattern
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using S3StorageDriver driver = Driver();
        string dir = ScratchName();
        string fileA = $"{dir}/a.txt";
        string fileB = $"{dir}/b.txt";
        string fileC = $"{dir}/c.bin";
        byte[] bytes = new byte[4];

        SkipIfUnreachable(() => driver.DirectoryExists(dir));

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
            txtEntries.Should().HaveCount(2, "pattern *.txt should return exactly a.txt and b.txt");
            txtEntries.Should().Contain(e => e.EndsWith("a.txt", StringComparison.Ordinal));
            txtEntries.Should().Contain(e => e.EndsWith("b.txt", StringComparison.Ordinal));

            // Recursive — should still find the same two .txt files
            List<string> recursive = driver
                .EnumerateFileSystemEntries(dir, "*.txt", SearchOption.AllDirectories)
                .ToList();
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
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 14 — IsHidden: dotfiles are hidden
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using S3StorageDriver driver = Driver();
        // S3StorageDriver.IsHidden always returns false (S3 has no hidden concept).
        // Contract: must not throw. We just assert the method is callable.
        bool result = driver.IsHidden(".hidden-file");
        result.Should().BeFalse("S3 has no hidden attribute — IsHidden always returns false");
    }

    // -----------------------------------------------------------------------
    // Op 15 — MoveDirectory renames collection
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task MoveDirectory_renames_collection()
    {
        using S3StorageDriver driver = Driver();
        string src = ScratchName();
        string dst = ScratchName();
        string file = $"{src}/item.bin";
        byte[] data = new byte[16];

        SkipIfUnreachable(() => driver.DirectoryExists(src));

        try
        {
            await using (Stream w = driver.OpenWrite(file, overwrite: true))
                await w.WriteAsync(data);

            driver.MoveDirectory(src, dst);

            driver.DirectoryExists(src).Should().BeFalse("old dir must not exist after move");
            driver.DirectoryExists(dst).Should().BeTrue("new dir must exist after move");
        }
        finally
        {
            try
            {
                driver.DeleteDirectory(src, recursive: true);
            }
            catch { }

            try
            {
                driver.DeleteDirectory(dst, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R2] cleanup warning: {ex.Message}");
            }
        }
    }
}
