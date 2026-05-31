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
    public void SizeOrZero_returns_zero_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);

        long result = storage.SizeOrZero("missing.bin");

        result.Should().Be(0);
        driver.Verify(b => b.GetFileSize(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SizeOrZero_returns_size_when_file_present()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(true);
        driver.Setup(b => b.GetFileSize(It.IsAny<string>())).Returns(2048);

        long result = storage.SizeOrZero("file.bin");

        result.Should().Be(2048);
    }

    [Fact]
    public void Exists_reports_file_or_directory()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        storage.Exists("some/dir").Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_calls_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        storage.CreateDirectory("nested/dir");

        driver.Verify(b => b.CreateDirectory(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Write_creates_parent_directory_when_missing_and_overwrites()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream sink = new();
        driver.Setup(b => b.OpenWrite(It.IsAny<string>(), true)).Returns(sink);

        storage.Write("nested/file.bin", [0x42, 0x43]);

        driver.Verify(b => b.CreateDirectory(It.IsAny<string>()), Times.Once);
        sink.ToArray().Should().Equal(0x42, 0x43);
    }

    [Fact]
    public void Read_pulls_full_stream_from_backend()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        byte[] payload = [0xAA, 0xBB, 0xCC];
        driver.Setup(b => b.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream(payload));

        storage.Read("file.bin").Should().Equal(payload);
    }

    [Fact]
    public void Delete_no_op_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);

        storage.Delete("missing.bin");

        driver.Verify(b => b.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Move_validates_both_paths_and_creates_destination_parent()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);

        storage.Move("a/file", "b/sub/file");

        driver.Verify(
            b => b.CreateDirectory(It.Is<string>(s => s.EndsWith(Path.Combine("b", "sub")))),
            Times.Once
        );
        driver.Verify(b => b.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Copy_uses_overwrite_true()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        driver.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        storage.Copy("src/a", "dst/b");

        driver.Verify(b => b.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public void AcquireLocalPath_returns_lease_with_canonical_path()
    {
        (LocalStorage storage, Mock<IStorageDriver> _) = Build();

        LocalPathLease lease = storage.AcquireLocalPath("some/file.bin");

        lease.Path.Should().Be(Path.GetFullPath("some/file.bin"));
    }

    [Fact]
    public void List_returns_entries_with_metadata()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();
        string root = Path.Combine(Path.GetTempPath(), "nm-listing-sync");
        string fileA = Path.Combine(root, "a.txt");
        string subDir = Path.Combine(root, "sub");

        driver
            .Setup(b =>
                b.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns([fileA, subDir]);
        driver.Setup(b => b.DirectoryExists(fileA)).Returns(false);
        driver.Setup(b => b.DirectoryExists(subDir)).Returns(true);
        driver.Setup(b => b.GetFileSize(fileA)).Returns(42);
        driver.Setup(b => b.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(DateTime.UtcNow);

        IReadOnlyList<StorageEntry> entries = storage.List(root, "*", recursive: false);

        entries.Should().HaveCount(2);
        // LocalStorage normalizes paths to forward-slash per the IStorage
        // Rule 2 contract — driver hands out OS-native paths but the
        // facade emits forward-slash for consumer uniformity.
        entries[0].Path.Should().Be(fileA.Replace('\\', '/'));
        entries[0].IsDirectory.Should().BeFalse();
        entries[0].SizeBytes.Should().Be(42);
        entries[1].IsDirectory.Should().BeTrue();
        entries[1].SizeBytes.Should().Be(0);
    }

    [Fact]
    public void Sync_methods_route_through_path_guard()
    {
        (LocalStorage storage, Mock<IStorageDriver> driver) = Build();

        Action act = () => storage.Read("bad\0path");

        act.Should().Throw<StoragePathNotAllowedException>();
        driver.Verify(b => b.OpenRead(It.IsAny<string>()), Times.Never);
    }
}
