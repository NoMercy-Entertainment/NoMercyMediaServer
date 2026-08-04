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

using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Storage;

/// <summary>
/// <see cref="LocalStorageDriver"/> is the only <see cref="IStorageDriver"/>
/// that touches the real OS filesystem directly (every other driver goes
/// over a network protocol). These tests exercise it against a REAL temp
/// directory rather than mocking <see cref="System.IO"/> — a mock of
/// <c>File</c>/<c>Directory</c> would prove nothing about whether the driver
/// actually reads/writes correctly.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LocalStorageDriverTests : IDisposable
{
    private readonly string _root;
    private readonly LocalStorageDriver _driver = new();

    public LocalStorageDriverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"nm-lsd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private string P(string relative) => Path.Combine(_root, relative);

    [Fact]
    public void BackendLabel_is_Local()
    {
        _driver.BackendLabel.Should().Be("Local");
    }

    [Fact]
    public void DirectorySeparator_is_the_OS_separator()
    {
        _driver.DirectorySeparator.Should().Be(Path.DirectorySeparatorChar);
    }

    [Fact]
    public void CombinePath_joins_parent_and_child()
    {
        _driver.CombinePath("a", "b").Should().Be(Path.Combine("a", "b"));
    }

    /// <summary>
    /// Every stored Filename starts with a separator, so a child that looks rooted is
    /// the normal case, not the exception. Path.Combine answers the child alone there —
    /// the folder silently disappears and the caller checks a path at the filesystem
    /// root. The IStorageDriver contract is a plain join.
    /// </summary>
    [Fact]
    public void CombinePath_keeps_the_parent_when_the_child_starts_with_a_separator()
    {
        _driver
            .CombinePath("Films/Fight Club (1999)", "/Fight Club (1999).NoMercy.m3u8")
            .Should()
            .Be(Path.Combine("Films/Fight Club (1999)", "Fight Club (1999).NoMercy.m3u8"));
    }

    [Fact]
    public void FileExists_and_DirectoryExists_distinguish_files_from_directories()
    {
        string filePath = P("file.txt");
        File.WriteAllText(filePath, "x");
        string dirPath = P("subdir");
        Directory.CreateDirectory(dirPath);

        _driver.FileExists(filePath).Should().BeTrue();
        _driver.FileExists(dirPath).Should().BeFalse();
        _driver.DirectoryExists(dirPath).Should().BeTrue();
        _driver.DirectoryExists(filePath).Should().BeFalse();
    }

    [Fact]
    public void CreateDirectory_creates_nested_directories()
    {
        string nested = P("a/b/c");

        _driver.CreateDirectory(nested);

        Directory.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void DeleteFile_removes_the_file()
    {
        string filePath = P("gone.txt");
        File.WriteAllText(filePath, "x");

        _driver.DeleteFile(filePath);

        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void DeleteDirectory_recursive_removes_the_subtree()
    {
        string dir = P("tree");
        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        File.WriteAllText(Path.Combine(dir, "nested", "f.txt"), "x");

        _driver.DeleteDirectory(dir, recursive: true);

        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_returns_byte_length()
    {
        string filePath = P("sized.bin");
        File.WriteAllBytes(filePath, new byte[321]);

        _driver.GetFileSize(filePath).Should().Be(321);
    }

    [Fact]
    public void GetLastWriteTimeUtc_reflects_recent_write()
    {
        string filePath = P("written.txt");
        File.WriteAllText(filePath, "x");

        DateTime result = _driver.GetLastWriteTimeUtc(filePath);

        result.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetCreationTimeUtc_works_for_both_files_and_directories()
    {
        string filePath = P("created.txt");
        File.WriteAllText(filePath, "x");
        string dirPath = P("createddir");
        Directory.CreateDirectory(dirPath);

        _driver
            .GetCreationTimeUtc(filePath)
            .Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _driver
            .GetCreationTimeUtc(dirPath)
            .Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void GetLastAccessTimeUtc_works_for_both_files_and_directories()
    {
        string filePath = P("accessed.txt");
        File.WriteAllText(filePath, "x");
        string dirPath = P("accesseddir");
        Directory.CreateDirectory(dirPath);

        _driver
            .GetLastAccessTimeUtc(filePath)
            .Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _driver
            .GetLastAccessTimeUtc(dirPath)
            .Should()
            .BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void OpenWrite_then_OpenRead_round_trips_bytes()
    {
        string filePath = P("roundtrip.bin");
        byte[] payload = [1, 2, 3, 4, 5];

        using (Stream write = _driver.OpenWrite(filePath, overwrite: true))
            write.Write(payload, 0, payload.Length);

        using Stream read = _driver.OpenRead(filePath);
        byte[] buffer = new byte[payload.Length];
        read.ReadExactly(buffer);

        buffer.Should().Equal(payload);
    }

    [Fact]
    public void OpenWrite_overwrite_false_throws_when_file_exists()
    {
        string filePath = P("exists.bin");
        File.WriteAllBytes(filePath, [0x01]);

        Action act = () =>
        {
            using Stream s = _driver.OpenWrite(filePath, overwrite: false);
        };

        act.Should().Throw<IOException>();
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_the_same_path_with_a_noop_lease()
    {
        string filePath = P("lease.txt");
        File.WriteAllText(filePath, "x");

        LocalPathLease lease = await _driver.AcquireLocalPathAsync(
            filePath,
            CancellationToken.None
        );

        lease.Path.Should().Be(filePath, "local files are already local; no staging copy is made");
        await lease.DisposeAsync();
        File.Exists(filePath)
            .Should()
            .BeTrue("the no-op lease must never delete the real local file");
    }

    [Fact]
    public void MoveFile_moves_and_removes_source()
    {
        string src = P("src.txt");
        string dst = P("dst.txt");
        File.WriteAllText(src, "payload");

        _driver.MoveFile(src, dst);

        File.Exists(src).Should().BeFalse();
        File.ReadAllText(dst).Should().Be("payload");
    }

    [Fact]
    public void CopyFile_duplicates_and_keeps_source()
    {
        string src = P("src.txt");
        string dst = P("dst.txt");
        File.WriteAllText(src, "payload");

        _driver.CopyFile(src, dst, overwrite: false);

        File.Exists(src).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("payload");
    }

    [Fact]
    public void CopyFile_overwrite_false_throws_when_destination_exists()
    {
        string src = P("src.txt");
        string dst = P("dst.txt");
        File.WriteAllText(src, "a");
        File.WriteAllText(dst, "b");

        Action act = () => _driver.CopyFile(src, dst, overwrite: false);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void EnumerateFileSystemEntries_on_missing_directory_returns_empty_not_throw()
    {
        string missing = P("does-not-exist");

        IEnumerable<string> result = _driver.EnumerateFileSystemEntries(
            missing,
            "*",
            SearchOption.TopDirectoryOnly
        );

        result
            .Should()
            .BeEmpty(
                "the path contract guarantees List on a missing directory returns empty, never throws"
            );
    }

    [Fact]
    public void EnumerateFileSystemEntries_lists_files_in_existing_directory()
    {
        File.WriteAllText(P("a.txt"), "x");
        File.WriteAllText(P("b.txt"), "x");

        IEnumerable<string> result = _driver.EnumerateFileSystemEntries(
            _root,
            "*.txt",
            SearchOption.TopDirectoryOnly
        );

        result.Should().HaveCount(2);
    }

    [Fact]
    public void EnumerateEntries_on_missing_directory_yields_nothing()
    {
        IStorageDriver driver = _driver;
        string missing = P("gone");

        List<StorageEntryInfo> entries = driver
            .EnumerateEntries(missing, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntries_returns_size_and_is_directory_for_each_entry_in_one_pass()
    {
        IStorageDriver driver = _driver;
        File.WriteAllBytes(P("file.bin"), new byte[50]);
        Directory.CreateDirectory(P("childdir"));

        List<StorageEntryInfo> entries = driver
            .EnumerateEntries(_root, "*", SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().HaveCount(2);
        entries.Single(e => !e.IsDirectory).Size.Should().Be(50);
        entries.Single(e => e.IsDirectory).Size.Should().Be(0);
    }

    [Fact]
    public void GetFullPath_canonicalizes_relative_segments()
    {
        string result = _driver.GetFullPath(Path.Combine(_root, "a", "..", "b.txt"));

        result.Should().Be(Path.GetFullPath(Path.Combine(_root, "a", "..", "b.txt")));
    }

    [Fact]
    public void ResolveLinkTarget_returns_null_for_a_plain_file()
    {
        string filePath = P("plain.txt");
        File.WriteAllText(filePath, "x");

        _driver.ResolveLinkTarget(filePath).Should().BeNull("a plain file is not a symlink");
    }

    [Fact]
    public void ResolveLinkTarget_returns_null_for_a_missing_path()
    {
        _driver.ResolveLinkTarget(P("missing")).Should().BeNull();
    }

    [SkippableFact]
    public void ResolveLinkTarget_follows_a_real_symlink_to_its_canonical_target()
    {
        string target = P("real-target.txt");
        File.WriteAllText(target, "x");
        string link = P("link.txt");

        bool canCreateSymlink = true;
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            canCreateSymlink = false;
        }
        Skip.IfNot(canCreateSymlink, "creating symlinks requires elevated privilege on this host");

        string? resolved = _driver.ResolveLinkTarget(link);

        resolved.Should().Be(Path.GetFullPath(target));
    }

    [Fact]
    public void IsHidden_returns_false_for_a_normal_visible_file()
    {
        string filePath = P("visible.txt");
        File.WriteAllText(filePath, "x");

        _driver.IsHidden(filePath).Should().BeFalse();
    }

    [Fact]
    public void IsHidden_returns_false_for_a_missing_path_instead_of_throwing()
    {
        _driver
            .IsHidden(P("missing"))
            .Should()
            .BeFalse("querying attributes on a missing path must not throw");
    }

    [SkippableFact]
    public void IsHidden_returns_true_when_the_hidden_attribute_is_set()
    {
        Skip.IfNot(
            OperatingSystem.IsWindows(),
            "FileAttributes.Hidden is a Windows-native concept for this test's setup"
        );
        string filePath = P("hidden.txt");
        File.WriteAllText(filePath, "x");
        File.SetAttributes(filePath, File.GetAttributes(filePath) | FileAttributes.Hidden);

        _driver.IsHidden(filePath).Should().BeTrue();
    }

    [Fact]
    public void MoveDirectory_moves_the_whole_subtree()
    {
        string src = P("srcdir");
        string dst = P("dstdir");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "f.txt"), "x");

        _driver.MoveDirectory(src, dst);

        Directory.Exists(src).Should().BeFalse();
        File.Exists(Path.Combine(dst, "f.txt")).Should().BeTrue();
    }
}
