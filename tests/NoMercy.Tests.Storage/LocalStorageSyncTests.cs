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
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Sync-companion API on <see cref="LocalStorage"/>. Async coverage lives
/// in <see cref="LocalStorageUnitTests"/>; this file just asserts the
/// sync surface delegates correctly and applies the same path guard.
/// </summary>
public class LocalStorageSyncTests
{
    private static (LocalStorage storage, Mock<IStorageDriver> driver) Build()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        driver
            .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
        driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);

        StoragePathGuard guard = new(allowedRoots: [], driver: driver.Object);
        LocalStorage storage = new(driver: driver.Object, guard: guard);
        return (storage, driver);
    }

    [Fact]
    public void SizeOrZero_returns_zero_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: false);

        long result = storage.SizeOrZero(path: "missing.bin");

        result.Should().Be(expected: 0);
        driver.Verify(expression: b => b.GetFileSize(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public void SizeOrZero_returns_size_when_file_present()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: true);
        driver.Setup(expression: b => b.GetFileSize(It.IsAny<string>())).Returns(value: 2048);

        long result = storage.SizeOrZero(path: "file.bin");

        result.Should().Be(expected: 2048);
    }

    [Fact]
    public void Exists_reports_file_or_directory()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: false);
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        storage.Exists(path: "some/dir").Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_calls_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        storage.CreateDirectory(path: "nested/dir");

        driver.Verify(expression: b => b.CreateDirectory(It.IsAny<string>()), times: Times.Once);
    }

    [Fact]
    public void Write_creates_parent_directory_when_missing_and_overwrites()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        MemoryStream sink = new();
        driver.Setup(expression: b => b.OpenWrite(It.IsAny<string>(), true)).Returns(value: sink);

        storage.Write(path: "nested/file.bin", bytes: [0x42, 0x43]);

        driver.Verify(expression: b => b.CreateDirectory(It.IsAny<string>()), times: Times.Once);
        sink.ToArray().Should().Equal(elements: [0x42, 0x43]);
    }

    [Fact]
    public void Read_pulls_full_stream_from_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        byte[] payload = [0xAA, 0xBB, 0xCC];
        driver.Setup(expression: b => b.OpenRead(It.IsAny<string>())).Returns(valueFunction: () => new MemoryStream(buffer: payload));

        storage.Read(path: "file.bin").Should().Equal(elements: payload);
    }

    [Fact]
    public void Delete_no_op_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: false);

        storage.Delete(path: "missing.bin");

        driver.Verify(expression: b => b.DeleteFile(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public void Move_validates_both_paths_and_creates_destination_parent()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        storage.Move(from: "a/file", to: "b/sub/file");

        driver.Verify(
            expression: b => b.CreateDirectory(It.Is<string>(s => s.EndsWith(Path.Combine("b", "sub")))),
            times: Times.Once
        );
        driver.Verify(expression: b => b.MoveFile(It.IsAny<string>(), It.IsAny<string>()), times: Times.Once);
    }

    [Fact]
    public void Copy_uses_overwrite_true()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        storage.Copy(from: "src/a", to: "dst/b");

        driver.Verify(expression: b => b.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true), times: Times.Once);
    }

    [Fact]
    public void AcquireLocalPath_returns_lease_with_canonical_path()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        LocalPathLease lease = storage.AcquireLocalPath(path: "some/file.bin");

        lease.Path.Should().Be(expected: Path.GetFullPath(path: "some/file.bin"));
    }

    [Fact]
    public void List_returns_entries_with_metadata()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        string root = Path.Combine(path1: Path.GetTempPath(), path2: "nm-listing-sync");
        string fileA = Path.Combine(path1: root, path2: "a.txt");
        string subDir = Path.Combine(path1: root, path2: "sub");

        driver
            .Setup(expression: b =>
                b.EnumerateEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns(value:
            [
                new StorageEntryInfo(Path: fileA, IsDirectory: false, Size: 42, LastWriteUtc: DateTime.UtcNow),
                new StorageEntryInfo(Path: subDir, IsDirectory: true, Size: 0, LastWriteUtc: DateTime.UtcNow),
            ]);

        IReadOnlyList<StorageEntry> entries = storage.List(path: root, pattern: "*", recursive: false);

        entries.Should().HaveCount(expected: 2);
        // LocalStorage normalizes paths to forward-slash per the IStorage
        // Rule 2 contract — driver hands out OS-native paths but the
        // facade emits forward-slash for consumer uniformity.
        entries[index: 0].Path.Should().Be(expected: fileA.Replace(oldChar: '\\', newChar: '/'));
        entries[index: 0].IsDirectory.Should().BeFalse();
        entries[index: 0].SizeBytes.Should().Be(expected: 42);
        entries[index: 1].IsDirectory.Should().BeTrue();
        entries[index: 1].SizeBytes.Should().Be(expected: 0);
    }

    [Fact]
    public void Sync_methods_route_through_path_guard()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        Action act = () => storage.Read(path: "bad\0path");

        act.Should().Throw<StoragePathNotAllowedException>();
        driver.Verify(expression: b => b.OpenRead(It.IsAny<string>()), times: Times.Never);
    }
}
