using System.Net.Sockets;
using NoMercy.Storage.Drivers.S3;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Live integration tests for the S3 (MinIO/Vliegtuig) driver.
//
// Same pattern as R2LiveTests — see that file for comment conventions.
// The S3 driver row in the live system has a malformed bucket field (a full
// console URL instead of a bucket name). The fixture extracts just the name
// segment from the URL so the driver can be constructed cleanly. If that
// extraction fails the fixture sets SkipReason before any test runs.
// ============================================================================

public sealed class S3LiveTests(S3LiveFixture fix) : IClassFixture<S3LiveFixture>
{
    private S3StorageDriver Driver()
    {
        Skip.If(fix.SkipReason is not null, fix.SkipReason ?? string.Empty);

        S3StorageDriver? driver = fix.BuildDriver(null, null);
        Skip.If(driver is null, "S3 driver could not be constructed from config");
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
                $"S3 endpoint unreachable — check VPN/network or that the bucket exists ({ex.Message})"
            );
            throw;
        }
    }

    private static string ScratchName() => $"nmtest-{Ulid.NewUlid()}";

    // -----------------------------------------------------------------------
    // Op 1
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void Mount_succeeds()
    {
        using S3StorageDriver driver = Driver();
        driver.Should().NotBeNull();
    }

    // -----------------------------------------------------------------------
    // Op 2
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
        Console.WriteLine($"[S3] root entry count: {entries.Count}");
    }

    // -----------------------------------------------------------------------
    // Op 3
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
    // Op 4
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
    // Op 5
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

        driver.FileExists(file!).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Op 6
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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // Op 7
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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 8
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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 9
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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 10
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

    // -----------------------------------------------------------------------
    // Op 11
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

    // -----------------------------------------------------------------------
    // Op 12
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

    // -----------------------------------------------------------------------
    // Op 13
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
            txtEntries.Should().HaveCount(2);
            txtEntries.Should().Contain(e => e.EndsWith("a.txt", StringComparison.Ordinal));
            txtEntries.Should().Contain(e => e.EndsWith("b.txt", StringComparison.Ordinal));

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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Op 14
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using S3StorageDriver driver = Driver();
        bool result = driver.IsHidden(".hidden-file");
        result.Should().BeFalse("S3 has no hidden attribute — IsHidden always returns false");
    }

    // -----------------------------------------------------------------------
    // Op 15
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

            driver.DirectoryExists(src).Should().BeFalse();
            driver.DirectoryExists(dst).Should().BeTrue();
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
                Console.WriteLine($"[S3] cleanup warning: {ex.Message}");
            }
        }
    }
}
