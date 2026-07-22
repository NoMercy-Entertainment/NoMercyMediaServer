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
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// NFS tests — target the shared StorageBackends container (kernel nfsd at
// 127.0.0.1, export "/"). The container seeds the following layout:
//
//   /Music/                                                  — directory
//   /Music/A/  /Music/B/ ... /Music/E/                       — directories
//   /Music/A/artist$/                                        — directory (literal $)
//   /Music/A/artist$/[2025] album$/                          — directory (brackets + $)
//   /Music/A/artist$/[2025] album$/01 track one.mp3          — file ("ID3" header)
//   /Music/A/artist$/[2025] album$/02 track two [feat. Tëst].mp3  — UTF-8 filename
//
// Tests skip when the container is unavailable or when the libnfs native
// library is not present, so they don't break CI on machines without it.
// ============================================================================

[Collection(name: "StorageBackends")]
public class NfsLiveNasTests(StorageBackendsFixture fix)
{
    private const string Skip_Unavailable =
        "StorageBackends container not available or libnfs native library missing";

    private const string KnownDir = "Music/A/artist$";
    private const string KnownDeepDir = "Music/A/artist$/[2025] album$";
    private const string KnownFile = "Music/A/artist$/[2025] album$/01 track one.mp3";
    private const string KnownUtf8File =
        "Music/A/artist$/[2025] album$/02 track two [feat. Tëst].mp3";

    private NfsStorageDriver Mount()
    {
        Skip.If(condition: !fix.Available, reason: Skip_Unavailable);
        NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(
            condition: driver is null,
            reason: "libnfs native library not loadable; container NFS driver unavailable"
        );
        try
        {
            return driver;
        }
        catch (DllNotFoundException ex)
        {
            Skip.If(condition: true, reason: $"libnfs native library not loadable: {ex.Message}");
            throw;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            Skip.If(condition: true, reason: $"NFS mount failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    [SkippableFact]
    public void Mount_succeeds()
    {
        using NfsStorageDriver driver = Mount();
        // Reaching here = mount completed without throwing
        driver.Should().NotBeNull();
    }

    [SkippableFact]
    public void EnumerateRoot_returns_entries()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries(directory: "/", searchPattern: "*", option: SearchOption.TopDirectoryOnly),
        ];
        entries.Should().NotBeEmpty();
    }

    [SkippableFact]
    public void EnumerateRoot_contains_Music_dir()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries(directory: "/", searchPattern: "*", option: SearchOption.TopDirectoryOnly),
        ];
        entries
            .Should()
            .Contain(predicate: e => e == "/Music" || e.EndsWith("/Music", StringComparison.Ordinal));
    }

    // --- Directory.Exists semantics — these are the bug-finder tests --------

    [SkippableFact]
    public void DirectoryExists_returns_true_for_root()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(path: "/").Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_top_level_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(path: "Music").Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_nested_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(path: KnownDir).Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_true_for_dir_with_brackets_and_dollar()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(path: KnownDeepDir).Should().BeTrue();
    }

    [SkippableFact]
    public void DirectoryExists_returns_false_for_nonexistent()
    {
        using NfsStorageDriver driver = Mount();
        driver.DirectoryExists(path: "does/not/exist").Should().BeFalse();
    }

    // --- File.Exists / Size ---------------------------------------------------

    [SkippableFact]
    public void FileExists_returns_true_for_known_file()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(path: KnownFile).Should().BeTrue();
    }

    [SkippableFact]
    public void FileExists_returns_false_for_dir()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(path: KnownDir).Should().BeFalse();
    }

    [SkippableFact]
    public void GetFileSize_returns_positive_for_known_file()
    {
        using NfsStorageDriver driver = Mount();
        driver.GetFileSize(path: KnownFile).Should().BeGreaterThan(expected: 0);
    }

    [SkippableFact]
    public void OpenRead_returns_mp3_with_id3_signature()
    {
        using NfsStorageDriver driver = Mount();
        using Stream s = driver.OpenRead(path: KnownFile);
        byte[] head = new byte[8];
        int read = s.Read(buffer: head, offset: 0, count: head.Length);
        read.Should().Be(expected: 8);
        // Either ID3v2 ("ID3...") or MP3 frame sync (0xFF 0xEx)
        bool isId3 = head[0] == 0x49 && head[1] == 0x44 && head[2] == 0x33;
        bool isSync = head[0] == 0xFF && (head[1] & 0xE0) == 0xE0;
        (isId3 || isSync).Should().BeTrue(because: $"unexpected header: {BitConverter.ToString(value: head)}");
    }

    // --- UTF-8 path handling --------------------------------------------------

    [SkippableFact]
    public void FileExists_returns_true_for_utf8_filename()
    {
        using NfsStorageDriver driver = Mount();
        driver.FileExists(path: KnownUtf8File).Should().BeTrue();
    }

    [SkippableFact]
    public void EnumerateDirectory_preserves_utf8_filenames()
    {
        using NfsStorageDriver driver = Mount();
        List<string> entries =
        [
            .. driver.EnumerateFileSystemEntries(directory: KnownDeepDir, searchPattern: "*", option: SearchOption.TopDirectoryOnly),
        ];

        entries
            .Should()
            .Contain(
                predicate: e => e.Contains("Tëst", StringComparison.Ordinal),
                because: "libnfs returns NFS3/4 names as UTF-8; CharSet.Ansi mangled them to mojibake"
            );
    }

    // --- Listing returns correct directory flag ------------------------------

    // --- RemoteStorage.List path (mirrors the API browser exactly) ----------

    [SkippableFact]
    public void RemoteStorage_List_marks_subdirs_correctly()
    {
        Skip.If(condition: !fix.Available, reason: Skip_Unavailable);
        using NfsStorageDriver driver = Mount();
        RemoteStorage rs = new(driver: driver);

        IReadOnlyList<StorageEntry> entries = rs.List(path: "Music", pattern: null, recursive: false);
        entries.Should().NotBeEmpty();

        // 'Music' on the export contains alphabet directories (A, B, ...).
        // Every direct child should report IsDirectory == true.
        IReadOnlyList<StorageEntry> dirs = [.. entries.Where(predicate: e => e.IsDirectory)];
        dirs.Should()
            .HaveCountGreaterThan(
                expected: 0,
                because: "RemoteStorage.List uses driver.DirectoryExists(entryPath) to set IsDirectory; "
                         + "if Stat64 returns wrong mode bits or fails for nested paths, this stays empty"
            );
    }

    [SkippableFact]
    public void ListDirectories_marks_subdirs_as_directories()
    {
        using NfsStorageDriver driver = Mount();
        List<(string Name, bool IsDirectory)> entries = driver.ListDirectories(relativePath: "Music/A/artist$");

        // The artist$ folder contains album subdirectories like "[2025] album$".
        // ListDirectories filters to only directories, so any returned entry
        // must be IsDirectory == true.
        entries.Should().NotBeEmpty();
        entries.Should().OnlyContain(predicate: e => e.IsDirectory);
        entries.Should().Contain(predicate: e => e.Name.Contains("[2025]"));
    }
}
