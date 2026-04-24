using Moq;
using NoMercy.Storage;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Sync-companion API on <see cref="LocalStorage"/>. Async coverage lives
/// in <see cref="LocalStorageUnitTests"/>; this file just asserts the
/// sync surface delegates correctly and applies the same path guard.
/// </summary>
public class LocalStorageSyncTests
{
    private static (LocalStorage storage, Mock<IStorageBackend> backend) Build()
    {
        Mock<IStorageBackend> backend = new(MockBehavior.Loose);
        backend
            .Setup(b => b.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        backend.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

        StoragePathGuard guard = new([], backend.Object);
        LocalStorage storage = new(backend.Object, guard);
        return (storage, backend);
    }

    [Fact]
    public void SizeOrZero_returns_zero_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);

        long result = storage.SizeOrZero("missing.bin");

        result.Should().Be(0);
        backend.Verify(b => b.GetFileSize(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SizeOrZero_returns_size_when_file_present()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.FileExists(It.IsAny<string>())).Returns(true);
        backend.Setup(b => b.GetFileSize(It.IsAny<string>())).Returns(2048);

        long result = storage.SizeOrZero("file.bin");

        result.Should().Be(2048);
    }

    [Fact]
    public void Exists_reports_file_or_directory()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);
        backend.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        storage.Exists("some/dir").Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_calls_backend()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();

        storage.CreateDirectory("nested/dir");

        backend.Verify(b => b.CreateDirectory(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Write_creates_parent_directory_when_missing_and_overwrites()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream sink = new();
        backend.Setup(b => b.OpenWrite(It.IsAny<string>(), true)).Returns(sink);

        storage.Write("nested/file.bin", [0x42, 0x43]);

        backend.Verify(b => b.CreateDirectory(It.IsAny<string>()), Times.Once);
        sink.ToArray().Should().Equal([0x42, 0x43]);
    }

    [Fact]
    public void Read_pulls_full_stream_from_backend()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        byte[] payload = [0xAA, 0xBB, 0xCC];
        backend.Setup(b => b.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream(payload));

        storage.Read("file.bin").Should().Equal(payload);
    }

    [Fact]
    public void Delete_no_op_when_file_missing()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.FileExists(It.IsAny<string>())).Returns(false);

        storage.Delete("missing.bin");

        backend.Verify(b => b.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Move_validates_both_paths_and_creates_destination_parent()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(false);

        storage.Move("a/file", "b/sub/file");

        backend.Verify(
            b => b.CreateDirectory(It.Is<string>(s => s.EndsWith(Path.Combine("b", "sub")))),
            Times.Once
        );
        backend.Verify(b => b.MoveFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Copy_uses_overwrite_true()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();
        backend.Setup(b => b.DirectoryExists(It.IsAny<string>())).Returns(true);

        storage.Copy("src/a", "dst/b");

        backend.Verify(b => b.CopyFile(It.IsAny<string>(), It.IsAny<string>(), true), Times.Once);
    }

    [Fact]
    public void AcquireLocalPath_returns_lease_with_canonical_path()
    {
        (LocalStorage storage, Mock<IStorageBackend> _) = Build();

        LocalPathLease lease = storage.AcquireLocalPath("some/file.bin");

        lease.Path.Should().Be(Path.GetFullPath("some/file.bin"));
    }

    [Fact]
    public void Sync_methods_route_through_path_guard()
    {
        (LocalStorage storage, Mock<IStorageBackend> backend) = Build();

        Action act = () => storage.Read("bad\0path");

        act.Should().Throw<StoragePathNotAllowedException>();
        backend.Verify(b => b.OpenRead(It.IsAny<string>()), Times.Never);
    }
}
