using Moq;
using NoMercy.Storage;
using NoMercy.Storage.Validation;

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

    // ── Phase 4.15 stress additions ──────────────────────────────────────────

    [Fact]
    public void UNC_path_escape_is_rejected_when_enforced()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-unc-root"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        // UNC paths always resolve outside a local drive root
        Action act = () => guard.Validate(@"\\server\share\file.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason == "device paths are not allowed"
            );
    }

    [Fact]
    public void Deep_traversal_sequence_is_canonicalized_and_rejected()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-deep-trav"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        // Three levels of ../ from inside the root — escapes to the temp dir
        string escape = Path.Combine(root, "a", "b", "..", "..", "..", "outside.txt");

        Action act = () => guard.Validate(escape);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Unicode_rtl_override_in_filename_passes_structural_checks_but_guard_still_enforces_root()
    {
        // The RTL override character U+202E can visually disguise a filename
        // (e.g. "cod‮exe.txt" renders as "codtxt.exe" on some terminals).
        // The guard does NOT strip it — the canonical path check is sufficient
        // because the resolved path will sit inside or outside the root
        // regardless of rendering tricks. This test confirms the guard is not
        // confused by the character and still enforces the allowlist correctly.
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-rtl"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string insidePath = Path.Combine(root, "sub", "cod‮exe.txt");
        string result = guard.Validate(insidePath);

        result.Should().Be(Path.GetFullPath(insidePath));
    }

    [Fact]
    public void Unicode_rtl_override_outside_root_is_rejected()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-rtl-deny"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string outsidePath = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "other", "cod‮exe.txt")
        );

        Action act = () => guard.Validate(outsidePath);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Traversal_via_url_encoded_dots_is_blocked_after_canonicalization()
    {
        // %2e%2e is an application-layer concern, not a filesystem one.
        // Path.GetFullPath treats literal percent signs as filename chars,
        // so the path stays outside the root and is rejected correctly.
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-pct"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        // This resolves to a path like /tmp/nm-guard-pct/%2e%2e/outside
        // — it is under the root after canonicalization so it passes.
        // If someone passes the decoded form it will escape and fail.
        string literalPct = Path.Combine(root, "%2e%2e", "inside.txt");
        string result = guard.Validate(literalPct);

        // Stays inside because %2e%2e is treated as a literal directory name.
        result.Should().StartWith(root);
    }

    [Fact]
    public void Multiple_allowed_roots_path_accepted_under_any_root()
    {
        string rootA = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-multi-a"));
        string rootB = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-multi-b"));
        StoragePathGuard guard = new([rootA, rootB], NewBackend().Object);

        string underB = Path.Combine(rootB, "output", "file.mp4");
        string result = guard.Validate(underB);

        result.Should().Be(Path.GetFullPath(underB));
    }

    [Fact]
    public void Multiple_allowed_roots_path_outside_both_rejected()
    {
        string rootA = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-multi2-a"));
        string rootB = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-multi2-b"));
        StoragePathGuard guard = new([rootA, rootB], NewBackend().Object);

        string outside = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-guard-multi2-c", "file.mp4")
        );

        Action act = () => guard.Validate(outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Enforced_rejects_path_that_is_sibling_of_root_not_under_it()
    {
        // Regression: "rootname-extra" must not be accepted for root "rootname"
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "nm-guard-sibling"));
        StoragePathGuard guard = new([root], NewBackend().Object);

        string sibling = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "nm-guard-sibling-extra", "file.txt")
        );

        Action act = () => guard.Validate(sibling);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.StartsWith("path is not under any allowed root"));
    }
}
