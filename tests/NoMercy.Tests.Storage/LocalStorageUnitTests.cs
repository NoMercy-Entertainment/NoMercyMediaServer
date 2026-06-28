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
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        driver
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

        StoragePathGuard guard = new([], driver.Object);
        LocalStorage storage = new(driver.Object, guard);
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
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        string root = Path.Combine(Path.GetTempPath(), $"nm-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            driver
                .Setup(b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(p => Path.GetFullPath(p));
            driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
            driver
                .Setup(b =>
                    b.EnumerateFileSystemEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns([]);
            driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);
            driver.Setup(b => b.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(DateTime.UtcNow);

            StoragePathGuard guard = new([root], driver.Object);
            LocalStorage storage = new(driver.Object, guard);

            IReadOnlyList<StorageEntry> entries = storage.List("", null, recursive: false);

            entries.Should().NotBeNull();
            driver.Verify(
                b =>
                    b.EnumerateFileSystemEntries(
                        It.Is<string>(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    ),
                Times.AtLeastOnce(),
                "empty path should resolve to the scoped root, not throw"
            );
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task ReadAsync_pulls_full_stream_from_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05];
        driver.Setup(b => b.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream(payload));

        byte[] result = await storage.ReadAsync("anywhere/file.bin", CancellationToken.None);

        result.Should().Equal(payload);
    }

    [Fact]
    public async Task WriteAsync_creates_parent_directory_when_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream sink = new();
        driver.Setup(b => b.OpenWrite(It.IsAny<string>(), true)).Returns(sink);

        await storage.WriteAsync("nested/dir/file.bin", [0xAA], CancellationToken.None);

        driver.Verify(b => b.CreateDirectory(It.IsAny<string>()), Times.Once);
        sink.ToArray().Should().Equal(0xAA);
    }

    [Fact]
    public async Task ExistsAsync_returns_true_for_file_or_directory()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        bool result = await storage.ExistsAsync("some/dir", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_no_op_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);

        await storage.DeleteAsync("missing.bin", CancellationToken.None);

        driver.Verify(b => b.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_calls_backend_when_file_present()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(true);

        await storage.DeleteAsync("present.bin", CancellationToken.None);

        driver.Verify(b => b.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task MoveAsync_validates_both_paths_and_ensures_parent()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);

        await storage.MoveAsync("a/file", "b/sub/file", CancellationToken.None);

        driver.Verify(
            b => b.CreateDirectory(It.Is<string>(s => s.EndsWith(Path.Combine("b", "sub")))),
            Times.Once
        );
        driver.Verify(b => b.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CopyAsync_uses_overwrite_true()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        await storage.CopyAsync("src/a", "dst/b", CancellationToken.None);

        driver.Verify(b => b.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public async Task SizeAsync_returns_backend_size()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.GetFileSize(It.IsAny<string>())).Returns(1234);

        long result = await storage.SizeAsync("file.bin", CancellationToken.None);

        result.Should().Be(1234);
    }

    [Fact]
    public async Task LastModifiedAsync_returns_utc_offset()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        DateTime utc = new(2026, 04, 24, 12, 00, 00, DateTimeKind.Utc);
        driver.Setup(b => b.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(utc);

        DateTimeOffset result = await storage.LastModifiedAsync("file.bin", CancellationToken.None);

        result.UtcDateTime.Should().Be(utc);
        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void EnumerateEntries_returns_one_pass_metadata_for_real_directory()
    {
        LocalStorageDriver driver = new();
        string root = Path.Combine(Path.GetTempPath(), $"nm-ee-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "a.txt"), "hello");
            Directory.CreateDirectory(Path.Combine(root, "sub"));

            List<StorageEntryInfo> entries = driver
                .EnumerateEntries(root, "*", SearchOption.TopDirectoryOnly)
                .ToList();

            entries.Should().HaveCount(2);
            StorageEntryInfo file = entries.Single(e => !e.IsDirectory);
            file.Path.Should().EndWith("a.txt");
            file.Size.Should().Be(5);
            StorageEntryInfo dir = entries.Single(e => e.IsDirectory);
            dir.Path.Should().EndWith("sub");
            dir.Size.Should().Be(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnumerateEntries_returns_empty_for_missing_directory()
    {
        LocalStorageDriver driver = new();
        string missing = Path.Combine(Path.GetTempPath(), $"nm-missing-{Guid.NewGuid():N}");

        driver.EnumerateEntries(missing, "*", SearchOption.TopDirectoryOnly).Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_yields_entries_with_correct_metadata()
    {
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        string root = Path.Combine(Path.GetTempPath(), $"nm-listing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string fileA = Path.Combine(root, "a.txt");
            string subDir = Path.Combine(root, "sub");

            driver
                .Setup(b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(p => Path.GetFullPath(p));
            driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
            driver.Setup(b => b.DirectoryExists(root)).Returns(true);
            driver
                .Setup(b =>
                    b.EnumerateEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns(
                    [
                        new StorageEntryInfo(fileA, false, 99, DateTime.UtcNow),
                        new StorageEntryInfo(subDir, true, 0, DateTime.UtcNow),
                    ]
                );

            StoragePathGuard guard = new([root], driver.Object);
            LocalStorage storage = new(driver.Object, guard);

            List<StorageEntry> result = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "",
                    "*",
                    recursive: false,
                    CancellationToken.None
                )
            )
                result.Add(e);

            result.Should().HaveCount(2);
            result[0]
                .Path.Should()
                .Be("a.txt", "List must return scope-relative paths, not OS-absolute");
            result[0]
                .Path.Should()
                .NotContain(":\\", "no Windows drive letter in scope-relative path");
            result[0].IsDirectory.Should().BeFalse();
            result[0].SizeBytes.Should().Be(99);
            result[1].Path.Should().Be("sub");
            result[1].IsDirectory.Should().BeTrue();
            result[1].SizeBytes.Should().Be(0);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public async Task HashAsync_unsupported_algorithm_throws()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        Func<Task> act = () => storage.HashAsync("x", "sha1", CancellationToken.None);

        await act.Should()
            .ThrowAsync<ArgumentException>()
            .Where(e => e.Message.Contains("unsupported hash algorithm"));
    }

    [Fact]
    public async Task HashAsync_sha256_matches_known_vector()
    {
        // SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver
            .Setup(b => b.OpenRead(It.IsAny<string>()))
            .Returns(() => new MemoryStream("abc"u8.ToArray()));

        string digest = await storage.HashAsync("file", "SHA256", CancellationToken.None);

        digest.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public async Task HashAsync_md5_matches_known_vector()
    {
        // MD5("") = d41d8cd98f00b204e9800998ecf8427e
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream([]));

        string digest = await storage.HashAsync("file", "md5", CancellationToken.None);

        digest.Should().Be("d41d8cd98f00b204e9800998ecf8427e");
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_lease_with_canonical_path_and_noop_dispose()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        await using LocalPathLease lease = await storage.AcquireLocalPathAsync(
            "some/file.bin",
            CancellationToken.None
        );

        lease.Path.Should().Be(Path.GetFullPath("some/file.bin"));
    }

    [Fact]
    public async Task Guard_rejects_invalid_path_before_backend_invoked()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        Func<Task> act = () => storage.ReadAsync("bad\0path", CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
        driver.Verify(b => b.OpenRead(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetFullPath_scope_relative_returns_os_absolute_under_root()
    {
        string root = Path.Combine(Path.GetTempPath(), $"nm-gfp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Mock<IStorageDriver> driver = new(MockBehavior.Loose);
            driver
                .Setup(b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(p => Path.GetFullPath(p));
            driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

            StoragePathGuard guard = new([root], driver.Object);
            IStorage storage = new LocalStorage(driver.Object, guard);

            string result = storage.GetFullPath("movies/avatar/avatar.mkv");

            Path.IsPathRooted(result)
                .Should()
                .BeTrue("GetFullPath must return an OS-absolute path");
            result
                .ToLowerInvariant()
                .Should()
                .StartWith(root.ToLowerInvariant(), "result must be under the root");
            result.Should().Contain("avatar.mkv");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public void GetFullPath_dotdot_traversal_throws_StoragePathNotAllowedException()
    {
        string root = Path.Combine(Path.GetTempPath(), $"nm-gfp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Mock<IStorageDriver> driver = new(MockBehavior.Loose);
            driver
                .Setup(b => b.GetFullPath(It.IsAny<string>()))
                .Returns<string>(p => Path.GetFullPath(p));
            driver.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

            StoragePathGuard guard = new([root], driver.Object);
            IStorage storage = new LocalStorage(driver.Object, guard);

            Action act = () => storage.GetFullPath("../escape/secret.txt");

            act.Should().Throw<StoragePathNotAllowedException>(".. traversal must be rejected");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }
}
