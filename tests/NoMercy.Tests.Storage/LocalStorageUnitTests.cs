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

public class LocalStorageUnitTests
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
    public void List_with_empty_path_lists_the_scoped_root_not_throws()
    {
        // Regression: dashboard StorageBrowserController passes empty path
        // when browsing the configured root of a local driver. Pre-fix,
        // StoragePathGuard rejected that with "path is empty" and the
        // browser surfaced "Storage list failed". Empty path now means
        // "the storage's scoped root".
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            driver
                .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
            driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
            driver
                .Setup(expression: b =>
                    b.EnumerateEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns(value: []);
            driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: true);

            StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
            LocalStorage storage = new(driver: driver.Object, guard: guard);

            IReadOnlyList<StorageEntry> entries = storage.List(path: "", pattern: null, recursive: false);

            entries.Should().NotBeNull();
            driver.Verify(
                expression: b =>
                    b.EnumerateEntries(
                        It.Is<string>(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    ),
                times: Times.AtLeastOnce(),
                failMessage: "empty path should resolve to the scoped root, not throw"
            );
        }
        finally
        {
            try
            {
                Directory.Delete(path: root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ReadAsync_pulls_full_stream_from_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05];
        driver.Setup(expression: b => b.OpenRead(It.IsAny<string>())).Returns(valueFunction: () => new MemoryStream(buffer: payload));

        byte[] result = await storage.ReadAsync(path: "anywhere/file.bin", ct: CancellationToken.None);

        result.Should().Equal(elements: payload);
    }

    [Fact]
    public async Task WriteAsync_creates_parent_directory_when_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        MemoryStream sink = new();
        driver.Setup(expression: b => b.OpenWrite(It.IsAny<string>(), true)).Returns(value: sink);

        await storage.WriteAsync(path: "nested/dir/file.bin", bytes: [0xAA], ct: CancellationToken.None);

        driver.Verify(expression: b => b.CreateDirectory(It.IsAny<string>()), times: Times.Once);
        sink.ToArray().Should().Equal(elements: 0xAA);
    }

    [Fact]
    public async Task ExistsAsync_returns_true_for_file_or_directory()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: false);
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        bool result = await storage.ExistsAsync(path: "some/dir", ct: CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_no_op_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: false);

        await storage.DeleteAsync(path: "missing.bin", ct: CancellationToken.None);

        driver.Verify(expression: b => b.DeleteFile(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_calls_backend_when_file_present()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.FileExists(It.IsAny<string>())).Returns(value: true);

        await storage.DeleteAsync(path: "present.bin", ct: CancellationToken.None);

        driver.Verify(expression: b => b.DeleteFile(It.IsAny<string>()), times: Times.Once);
    }

    [Fact]
    public async Task MoveAsync_validates_both_paths_and_ensures_parent()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        await storage.MoveAsync(from: "a/file", to: "b/sub/file", ct: CancellationToken.None);

        driver.Verify(
            expression: b => b.CreateDirectory(It.Is<string>(s => s.EndsWith(Path.Combine("b", "sub")))),
            times: Times.Once
        );
        driver.Verify(expression: b => b.MoveFile(It.IsAny<string>(), It.IsAny<string>()), times: Times.Once);
    }

    [Fact]
    public async Task CopyAsync_uses_overwrite_true()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        await storage.CopyAsync(from: "src/a", to: "dst/b", ct: CancellationToken.None);

        driver.Verify(expression: b => b.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true), times: Times.Once);
    }

    [Fact]
    public async Task SizeAsync_returns_backend_size()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.GetFileSize(It.IsAny<string>())).Returns(value: 1234);

        long result = await storage.SizeAsync(path: "file.bin", ct: CancellationToken.None);

        result.Should().Be(expected: 1234);
    }

    [Fact]
    public async Task LastModifiedAsync_returns_utc_offset()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        DateTime utc = new(year: 2026, month: 04, day: 24, hour: 12, minute: 00, second: 00, kind: DateTimeKind.Utc);
        driver.Setup(expression: b => b.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(value: utc);

        DateTimeOffset result = await storage.LastModifiedAsync(path: "file.bin", ct: CancellationToken.None);

        result.UtcDateTime.Should().Be(expected: utc);
        result.Offset.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public void EnumerateEntries_returns_one_pass_metadata_for_real_directory()
    {
        LocalStorageDriver driver = new();
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-ee-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            File.WriteAllText(path: Path.Combine(path1: root, path2: "a.txt"), contents: "hello");
            Directory.CreateDirectory(path: Path.Combine(path1: root, path2: "sub"));

            List<StorageEntryInfo> entries = driver
                .EnumerateEntries(directory: root, searchPattern: "*", option: SearchOption.TopDirectoryOnly)
                .ToList();

            entries.Should().HaveCount(expected: 2);
            StorageEntryInfo file = entries.Single(predicate: e => !e.IsDirectory);
            file.Path.Should().EndWith(expected: "a.txt");
            file.Size.Should().Be(expected: 5);
            StorageEntryInfo dir = entries.Single(predicate: e => e.IsDirectory);
            dir.Path.Should().EndWith(expected: "sub");
            dir.Size.Should().Be(expected: 0);
        }
        finally
        {
            Directory.Delete(path: root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateEntries_returns_empty_for_missing_directory()
    {
        LocalStorageDriver driver = new();
        string missing = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-missing-{Guid.NewGuid():N}");

        driver.EnumerateEntries(directory: missing, searchPattern: "*", option: SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_yields_entries_with_correct_metadata()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-listing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            string fileA = Path.Combine(path1: root, path2: "a.txt");
            string subDir = Path.Combine(path1: root, path2: "sub");

            driver
                .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
            driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
            driver.Setup(expression: b => b.DirectoryExists(root)).Returns(value: true);
            driver
                .Setup(expression: b =>
                    b.EnumerateEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns(value:
                [
                    new(Path: fileA, IsDirectory: false, Size: 99, LastWriteUtc: DateTime.UtcNow),
                    new(Path: subDir, IsDirectory: true, Size: 0, LastWriteUtc: DateTime.UtcNow),
                ]);

            StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
            LocalStorage storage = new(driver: driver.Object, guard: guard);

            List<StorageEntry> result = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "",
                    pattern: "*",
                    recursive: false,
                    ct: CancellationToken.None
                )
            )
                result.Add(item: e);

            result.Should().HaveCount(expected: 2);
            result[index: 0]
                .Path.Should()
                .Be(expected: "a.txt", because: "List must return scope-relative paths, not OS-absolute");
            result[index: 0]
                .Path.Should()
                .NotContain(unexpected: ":\\", because: "no Windows drive letter in scope-relative path");
            result[index: 0].IsDirectory.Should().BeFalse();
            result[index: 0].SizeBytes.Should().Be(expected: 99);
            result[index: 1].Path.Should().Be(expected: "sub");
            result[index: 1].IsDirectory.Should().BeTrue();
            result[index: 1].SizeBytes.Should().Be(expected: 0);
        }
        finally
        {
            try
            {
                Directory.Delete(path: root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task HashAsync_unsupported_algorithm_throws()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        Func<Task> act = () => storage.HashAsync(path: "x", algorithm: "sha1", ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .Where(exceptionExpression: e => e.Message.Contains("unsupported hash algorithm"));
    }

    [Fact]
    public async Task HashAsync_sha256_matches_known_vector()
    {
        // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver
            .Setup(expression: b => b.OpenRead(It.IsAny<string>()))
            .Returns(valueFunction: () => new MemoryStream(buffer: "abc"u8.ToArray()));

        string digest = await storage.HashAsync(path: "file", algorithm: "SHA256", ct: CancellationToken.None);

        digest.Should().Be(expected: "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public async Task HashAsync_md5_matches_known_vector()
    {
        // MD5("") = d41d8cd98f00b204e9800998ecf8427e
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(expression: b => b.OpenRead(It.IsAny<string>())).Returns(valueFunction: () => new MemoryStream(buffer: []));

        string digest = await storage.HashAsync(path: "file", algorithm: "md5", ct: CancellationToken.None);

        digest.Should().Be(expected: "d41d8cd98f00b204e9800998ecf8427e");
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_lease_with_canonical_path_and_noop_dispose()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        await using LocalPathLease lease = await storage.AcquireLocalPathAsync(
            path: "some/file.bin",
            ct: CancellationToken.None
        );

        lease.Path.Should().Be(expected: Path.GetFullPath(path: "some/file.bin"));
    }

    [Fact]
    public async Task Guard_rejects_invalid_path_before_backend_invoked()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        Func<Task> act = () => storage.ReadAsync(path: "bad\0path", ct: CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
        driver.Verify(expression: b => b.OpenRead(It.IsAny<string>()), times: Times.Never);
    }

    [Fact]
    public void GetFullPath_scope_relative_returns_os_absolute_under_root()
    {
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-gfp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
            driver
                .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
            driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);

            StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
            IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

            string result = storage.GetFullPath(path: "movies/avatar/avatar.mkv");

            Path.IsPathRooted(path: result)
                .Should()
                .BeTrue(because: "GetFullPath must return an OS-absolute path");
            result
                .ToLowerInvariant()
                .Should()
                .StartWith(expected: root.ToLowerInvariant(), because: "result must be under the root");
            result.Should().Contain(expected: "avatar.mkv");
        }
        finally
        {
            try
            {
                Directory.Delete(path: root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public void GetFullPath_dotdot_traversal_throws_StoragePathNotAllowedException()
    {
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-gfp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
            driver
                .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
            driver.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);

            StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
            IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

            Action act = () => storage.GetFullPath(path: "../escape/secret.txt");

            act.Should().Throw<StoragePathNotAllowedException>(because: ".. traversal must be rejected");
        }
        finally
        {
            try
            {
                Directory.Delete(path: root, recursive: true);
            }
            catch { }
        }
    }
}
