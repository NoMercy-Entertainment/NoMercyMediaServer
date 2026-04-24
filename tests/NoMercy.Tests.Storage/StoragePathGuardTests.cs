using Moq;
using NoMercy.Storage;

namespace NoMercy.Tests.Storage;

public class StoragePathGuardTests
{
    private static Mock<IStorageBackend> NewBackend()
    {
        Mock<IStorageBackend> m = new(MockBehavior.Strict);
        m.Setup(b => b.GetFullPath(It.IsAny<string>())).Returns<string>(p => Path.GetFullPath(p));
        m.Setup(b => b.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
        return m;
    }

    [Fact]
    public void Empty_path_is_rejected()
    {
        StoragePathGuard guard = new([], NewBackend().Object);

        Action act = () => guard.Validate("");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "path is empty");
    }

    [Fact]
    public void Whitespace_path_is_rejected()
    {
        StoragePathGuard guard = new([], NewBackend().Object);

        Action act = () => guard.Validate("   ");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "path is empty");
    }

    [Fact]
    public void Null_byte_is_rejected()
    {
        StoragePathGuard guard = new([], NewBackend().Object);

        Action act = () => guard.Validate("foo\0bar");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "null byte in path");
    }

    [Fact]
    public void Windows_device_paths_are_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;
        StoragePathGuard guard = new([], NewBackend().Object);

        Action questionDevice = () => guard.Validate(@"\\?\C:\Windows\System32");
        Action dotDevice = () => guard.Validate(@"\\.\PhysicalDrive0");

        questionDevice
            .Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "device paths are not allowed");
        dotDevice
            .Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void Permissive_when_allowed_roots_empty()
    {
        StoragePathGuard guard = new([], NewBackend().Object);

        string full = Path.GetFullPath("some/nested/file.txt");
        string result = guard.Validate(full);

        result.Should().Be(full);
        guard.Enforced.Should().BeFalse();
    }

    [Fact]
    public void Enforced_accepts_path_under_root()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-root-ok"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string inner = Path.Combine(root, "sub", "file.txt");
        string result = guard.Validate(inner);

        result.Should().Be(Path.GetFullPath(inner));
        guard.Enforced.Should().BeTrue();
    }

    [Fact]
    public void Enforced_rejects_path_outside_root()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-root-deny"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string outside = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "somewhere-else", "file.txt")
        );

        Action act = () => guard.Validate(outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Enforced_rejects_traversal_escape()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-root-trav"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        // ../ escapes beyond the root after canonicalization
        string escape = Path.Combine(root, "..", "outside.txt");

        Action act = () => guard.Validate(escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Enforced_rejects_when_symlink_target_escapes()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-link-root"));
        string canonicalInside = Path.Combine(root, "link");
        string realOutside = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "elsewhere", "target.txt")
        );

        Mock<IStorageBackend> backend = NewBackend();
        backend
            .Setup(b => b.ResolveLinkTarget(Path.GetFullPath(canonicalInside)))
            .Returns(realOutside);

        StoragePathGuard guard = new([root], backend.Object);

        Action act = () => guard.Validate(canonicalInside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Root_itself_is_allowed()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-root-self"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string result = guard.Validate(root);

        result.Should().Be(root);
    }

    [Fact]
    public void Roots_are_deduplicated_after_normalization()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-dedup"));
        string withTrailing = root + Path.DirectorySeparatorChar;

        StoragePathGuard guard = new([root, withTrailing], NewBackend().Object);

        guard.AllowedRoots.Should().HaveCount(1);
    }
}
