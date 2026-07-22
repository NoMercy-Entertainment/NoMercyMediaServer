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
[Trait(name: "Category", value: "Unit")]
public sealed class LocalStorageDriverTests : IDisposable
{
    private readonly string _root;
    private readonly LocalStorageDriver _driver = new();

    public LocalStorageDriverTests()
    {
        _root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-lsd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(path: _root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private string P(string relative) => Path.Combine(path1: _root, path2: relative);

    [Fact]
    public void BackendLabel_is_Local()
    {
        _driver.BackendLabel.Should().Be(expected: "Local");
    }

    [Fact]
    public void DirectorySeparator_is_the_OS_separator()
    {
        _driver.DirectorySeparator.Should().Be(expected: Path.DirectorySeparatorChar);
    }

    [Fact]
    public void CombinePath_delegates_to_Path_Combine()
    {
        _driver.CombinePath(parent: "a", child: "b").Should().Be(expected: Path.Combine(path1: "a", path2: "b"));
    }

    [Fact]
    public void FileExists_and_DirectoryExists_distinguish_files_from_directories()
    {
        string filePath = P(relative: "file.txt");
        File.WriteAllText(path: filePath, contents: "x");
        string dirPath = P(relative: "subdir");
        Directory.CreateDirectory(path: dirPath);

        _driver.FileExists(path: filePath).Should().BeTrue();
        _driver.FileExists(path: dirPath).Should().BeFalse();
        _driver.DirectoryExists(path: dirPath).Should().BeTrue();
        _driver.DirectoryExists(path: filePath).Should().BeFalse();
    }

    [Fact]
    public void CreateDirectory_creates_nested_directories()
    {
        string nested = P(relative: "a/b/c");

        _driver.CreateDirectory(path: nested);

        Directory.Exists(path: nested).Should().BeTrue();
    }

    [Fact]
    public void DeleteFile_removes_the_file()
    {
        string filePath = P(relative: "gone.txt");
        File.WriteAllText(path: filePath, contents: "x");

        _driver.DeleteFile(path: filePath);

        File.Exists(path: filePath).Should().BeFalse();
    }

    [Fact]
    public void DeleteDirectory_recursive_removes_the_subtree()
    {
        string dir = P(relative: "tree");
        Directory.CreateDirectory(path: Path.Combine(path1: dir, path2: "nested"));
        File.WriteAllText(path: Path.Combine(path1: dir, path2: "nested", path3: "f.txt"), contents: "x");

        _driver.DeleteDirectory(path: dir, recursive: true);

        Directory.Exists(path: dir).Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_returns_byte_length()
    {
        string filePath = P(relative: "sized.bin");
        File.WriteAllBytes(path: filePath, bytes: new byte[321]);

        _driver.GetFileSize(path: filePath).Should().Be(expected: 321);
    }

    [Fact]
    public void GetLastWriteTimeUtc_reflects_recent_write()
    {
        string filePath = P(relative: "written.txt");
        File.WriteAllText(path: filePath, contents: "x");

        DateTime result = _driver.GetLastWriteTimeUtc(path: filePath);

        result.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
    }

    [Fact]
    public void GetCreationTimeUtc_works_for_both_files_and_directories()
    {
        string filePath = P(relative: "created.txt");
        File.WriteAllText(path: filePath, contents: "x");
        string dirPath = P(relative: "createddir");
        Directory.CreateDirectory(path: dirPath);

        _driver
            .GetCreationTimeUtc(path: filePath)
            .Should()
            .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
        _driver
            .GetCreationTimeUtc(path: dirPath)
            .Should()
            .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
    }

    [Fact]
    public void GetLastAccessTimeUtc_works_for_both_files_and_directories()
    {
        string filePath = P(relative: "accessed.txt");
        File.WriteAllText(path: filePath, contents: "x");
        string dirPath = P(relative: "accesseddir");
        Directory.CreateDirectory(path: dirPath);

        _driver
            .GetLastAccessTimeUtc(path: filePath)
            .Should()
            .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
        _driver
            .GetLastAccessTimeUtc(path: dirPath)
            .Should()
            .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
    }

    [Fact]
    public void OpenWrite_then_OpenRead_round_trips_bytes()
    {
        string filePath = P(relative: "roundtrip.bin");
        byte[] payload = [1, 2, 3, 4, 5];

        using (Stream write = _driver.OpenWrite(path: filePath, overwrite: true))
            write.Write(buffer: payload, offset: 0, count: payload.Length);

        using Stream read = _driver.OpenRead(path: filePath);
        byte[] buffer = new byte[payload.Length];
        read.ReadExactly(buffer: buffer);

        buffer.Should().Equal(elements: payload);
    }

    [Fact]
    public void OpenWrite_overwrite_false_throws_when_file_exists()
    {
        string filePath = P(relative: "exists.bin");
        File.WriteAllBytes(path: filePath, bytes: [0x01]);

        Action act = () =>
        {
            using Stream s = _driver.OpenWrite(path: filePath, overwrite: false);
        };

        act.Should().Throw<IOException>();
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_the_same_path_with_a_noop_lease()
    {
        string filePath = P(relative: "lease.txt");
        File.WriteAllText(path: filePath, contents: "x");

        LocalPathLease lease = await _driver.AcquireLocalPathAsync(
            path: filePath,
            ct: CancellationToken.None
        );

        lease.Path.Should().Be(expected: filePath, because: "local files are already local; no staging copy is made");
        await lease.DisposeAsync();
        File.Exists(path: filePath)
            .Should()
            .BeTrue(because: "the no-op lease must never delete the real local file");
    }

    [Fact]
    public void MoveFile_moves_and_removes_source()
    {
        string src = P(relative: "src.txt");
        string dst = P(relative: "dst.txt");
        File.WriteAllText(path: src, contents: "payload");

        _driver.MoveFile(source: src, destination: dst);

        File.Exists(path: src).Should().BeFalse();
        File.ReadAllText(path: dst).Should().Be(expected: "payload");
    }

    [Fact]
    public void CopyFile_duplicates_and_keeps_source()
    {
        string src = P(relative: "src.txt");
        string dst = P(relative: "dst.txt");
        File.WriteAllText(path: src, contents: "payload");

        _driver.CopyFile(source: src, destination: dst, overwrite: false);

        File.Exists(path: src).Should().BeTrue();
        File.ReadAllText(path: dst).Should().Be(expected: "payload");
    }

    [Fact]
    public void CopyFile_overwrite_false_throws_when_destination_exists()
    {
        string src = P(relative: "src.txt");
        string dst = P(relative: "dst.txt");
        File.WriteAllText(path: src, contents: "a");
        File.WriteAllText(path: dst, contents: "b");

        Action act = () => _driver.CopyFile(source: src, destination: dst, overwrite: false);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void EnumerateFileSystemEntries_on_missing_directory_returns_empty_not_throw()
    {
        string missing = P(relative: "does-not-exist");

        IEnumerable<string> result = _driver.EnumerateFileSystemEntries(
            directory: missing,
            searchPattern: "*",
            option: SearchOption.TopDirectoryOnly
        );

        result
            .Should()
            .BeEmpty(
                because: "the path contract guarantees List on a missing directory returns empty, never throws"
            );
    }

    [Fact]
    public void EnumerateFileSystemEntries_lists_files_in_existing_directory()
    {
        File.WriteAllText(path: P(relative: "a.txt"), contents: "x");
        File.WriteAllText(path: P(relative: "b.txt"), contents: "x");

        IEnumerable<string> result = _driver.EnumerateFileSystemEntries(
            directory: _root,
            searchPattern: "*.txt",
            option: SearchOption.TopDirectoryOnly
        );

        result.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void EnumerateEntries_on_missing_directory_yields_nothing()
    {
        IStorageDriver driver = _driver;
        string missing = P(relative: "gone");

        List<StorageEntryInfo> entries = driver
            .EnumerateEntries(directory: missing, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntries_returns_size_and_is_directory_for_each_entry_in_one_pass()
    {
        IStorageDriver driver = _driver;
        File.WriteAllBytes(path: P(relative: "file.bin"), bytes: new byte[50]);
        Directory.CreateDirectory(path: P(relative: "childdir"));

        List<StorageEntryInfo> entries = driver
            .EnumerateEntries(directory: _root, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
            .ToList();

        entries.Should().HaveCount(expected: 2);
        entries.Single(predicate: e => !e.IsDirectory).Size.Should().Be(expected: 50);
        entries.Single(predicate: e => e.IsDirectory).Size.Should().Be(expected: 0);
    }

    [Fact]
    public void GetFullPath_canonicalizes_relative_segments()
    {
        string result = _driver.GetFullPath(path: Path.Combine(path1: _root, path2: "a", path3: "..", path4: "b.txt"));

        result.Should().Be(expected: Path.GetFullPath(path: Path.Combine(path1: _root, path2: "a", path3: "..", path4: "b.txt")));
    }

    [Fact]
    public void ResolveLinkTarget_returns_null_for_a_plain_file()
    {
        string filePath = P(relative: "plain.txt");
        File.WriteAllText(path: filePath, contents: "x");

        _driver.ResolveLinkTarget(path: filePath).Should().BeNull(because: "a plain file is not a symlink");
    }

    [Fact]
    public void ResolveLinkTarget_returns_null_for_a_missing_path()
    {
        _driver.ResolveLinkTarget(path: P(relative: "missing")).Should().BeNull();
    }

    [SkippableFact]
    public void ResolveLinkTarget_follows_a_real_symlink_to_its_canonical_target()
    {
        string target = P(relative: "real-target.txt");
        File.WriteAllText(path: target, contents: "x");
        string link = P(relative: "link.txt");

        bool canCreateSymlink = true;
        try
        {
            File.CreateSymbolicLink(path: link, pathToTarget: target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            canCreateSymlink = false;
        }
        Skip.IfNot(condition: canCreateSymlink, reason: "creating symlinks requires elevated privilege on this host");

        string? resolved = _driver.ResolveLinkTarget(path: link);

        resolved.Should().Be(expected: Path.GetFullPath(path: target));
    }

    [Fact]
    public void IsHidden_returns_false_for_a_normal_visible_file()
    {
        string filePath = P(relative: "visible.txt");
        File.WriteAllText(path: filePath, contents: "x");

        _driver.IsHidden(path: filePath).Should().BeFalse();
    }

    [Fact]
    public void IsHidden_returns_false_for_a_missing_path_instead_of_throwing()
    {
        _driver
            .IsHidden(path: P(relative: "missing"))
            .Should()
            .BeFalse(because: "querying attributes on a missing path must not throw");
    }

    [SkippableFact]
    public void IsHidden_returns_true_when_the_hidden_attribute_is_set()
    {
        Skip.IfNot(
            condition: OperatingSystem.IsWindows(),
            reason: "FileAttributes.Hidden is a Windows-native concept for this test's setup"
        );
        string filePath = P(relative: "hidden.txt");
        File.WriteAllText(path: filePath, contents: "x");
        File.SetAttributes(path: filePath, fileAttributes: File.GetAttributes(path: filePath) | FileAttributes.Hidden);

        _driver.IsHidden(path: filePath).Should().BeTrue();
    }

    [Fact]
    public void MoveDirectory_moves_the_whole_subtree()
    {
        string src = P(relative: "srcdir");
        string dst = P(relative: "dstdir");
        Directory.CreateDirectory(path: src);
        File.WriteAllText(path: Path.Combine(path1: src, path2: "f.txt"), contents: "x");

        _driver.MoveDirectory(source: src, destination: dst);

        Directory.Exists(path: src).Should().BeFalse();
        File.Exists(path: Path.Combine(path1: dst, path2: "f.txt")).Should().BeTrue();
    }
}
