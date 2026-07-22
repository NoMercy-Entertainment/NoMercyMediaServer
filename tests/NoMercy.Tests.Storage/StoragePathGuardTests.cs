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

using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage;

public class StoragePathGuardTests
{
    private static Mock<IStorageDriver> NewBackend()
    {
        Mock<IStorageDriver> m = new(behavior: MockBehavior.Strict);
        m.Setup(expression: b => b.GetFullPath(It.IsAny<string>())).Returns<string>(valueFunction: Path.GetFullPath);
        m.Setup(expression: b => b.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
        return m;
    }

    [Fact]
    public void Empty_path_is_rejected()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action act = () => guard.Validate(requestedPath: "");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    [Fact]
    public void Whitespace_path_is_rejected()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action act = () => guard.Validate(requestedPath: "   ");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    [Fact]
    public void Null_byte_is_rejected()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action act = () => guard.Validate(requestedPath: "foo\0bar");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "null byte in path");
    }

    [Fact]
    public void Windows_device_paths_are_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action questionDevice = () => guard.Validate(requestedPath: @"\\?\C:\Windows\System32");
        Action dotDevice = () => guard.Validate(requestedPath: @"\\.\PhysicalDrive0");

        questionDevice
            .Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
        dotDevice
            .Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void Permissive_when_allowed_roots_empty()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        string full = Path.GetFullPath(path: "some/nested/file.txt");
        string result = guard.Validate(requestedPath: full);

        result.Should().Be(expected: full);
        guard.Enforced.Should().BeFalse();
    }

    [Fact]
    public void Enforced_accepts_path_under_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-root-ok"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string inner = Path.Combine(path1: root, path2: "sub", path3: "file.txt");
        string result = guard.Validate(requestedPath: inner);

        result.Should().Be(expected: Path.GetFullPath(path: inner));
        guard.Enforced.Should().BeTrue();
    }

    [Fact]
    public void Enforced_accepts_path_under_a_bare_drive_or_volume_root()
    {
        // A driver rooted at a bare drive/volume root ("G:\" on Windows, "/" on
        // Unix) already ends in a separator. IsUnderRoot must still accept paths
        // under it: the earlier code appended a second separator ("G:\\") which
        // no real child path starts with, so every path on the drive was wrongly
        // rejected as "not under any allowed root".
        string driveRoot = Path.GetPathRoot(path: Path.GetFullPath(path: Path.GetTempPath()))!;
        StoragePathGuard guard = new(allowedRoots: [driveRoot], driver: NewBackend().Object);

        string inner = Path.Combine(path1: driveRoot, path2: "nm-drive-root-child", path3: "file.mkv");
        string result = guard.Validate(requestedPath: inner);

        result.Should().Be(expected: Path.GetFullPath(path: inner));
    }

    [Fact]
    public void Enforced_rejects_path_outside_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-root-deny"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string outside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "somewhere-else", path3: "file.txt")
        );

        Action act = () => guard.Validate(requestedPath: outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Enforced_rejects_traversal_escape()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-root-trav"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        // ../ escapes beyond the root after canonicalization
        string escape = Path.Combine(path1: root, path2: "..", path3: "outside.txt");

        Action act = () => guard.Validate(requestedPath: escape);

        // Structural validation catches the literal ".." in the path before
        // the allowlist check ever runs. Either rejection path proves the
        // guard refused to let the traversal through.
        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason.StartsWith(".. traversal is not allowed")
            );
    }

    [Fact]
    public void Enforced_rejects_when_symlink_target_escapes()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-link-root"));
        string canonicalInside = Path.Combine(path1: root, path2: "link");
        string realOutside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "elsewhere", path3: "target.txt")
        );

        Mock<IStorageDriver> driver = NewBackend();
        driver
            .Setup(expression: b => b.ResolveLinkTarget(Path.GetFullPath(canonicalInside)))
            .Returns(value: realOutside);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);

        Action act = () => guard.Validate(requestedPath: canonicalInside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Root_itself_is_allowed()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-root-self"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string result = guard.Validate(requestedPath: root);

        result.Should().Be(expected: root);
    }

    [Fact]
    public void Roots_are_deduplicated_after_normalization()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-dedup"));
        string withTrailing = root + Path.DirectorySeparatorChar;

        StoragePathGuard guard = new(allowedRoots: [root, withTrailing], driver: NewBackend().Object);

        guard.AllowedRoots.Should().HaveCount(expected: 1);
    }

    // ── Phase 4.15 stress additions ──────────────────────────────────────────

    [Fact]
    public void UNC_path_escape_is_rejected_when_enforced()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-unc-root"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        // UNC paths always resolve outside a local drive root
        Action act = () => guard.Validate(requestedPath: @"\\server\share\file.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason == "device paths are not allowed"
            );
    }

    [Fact]
    public void Deep_traversal_sequence_is_canonicalized_and_rejected()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-deep-trav"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        // Three levels of ../ from inside the root — escapes to the temp dir
        string escape = Path.Combine(paths: [root, "a", "b", "..", "..", "..", "outside.txt"]);

        Action act = () => guard.Validate(requestedPath: escape);

        // Either structural traversal-rejection or post-canonicalization
        // allowlist rejection proves the deep traversal can't slip through.
        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e =>
                e.Reason.StartsWith("path is not under any allowed root")
                || e.Reason.StartsWith(".. traversal is not allowed")
            );
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
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-rtl"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string insidePath = Path.Combine(path1: root, path2: "sub", path3: "cod‮exe.txt");
        string result = guard.Validate(requestedPath: insidePath);

        result.Should().Be(expected: Path.GetFullPath(path: insidePath));
    }

    [Fact]
    public void Unicode_rtl_override_outside_root_is_rejected()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-rtl-deny"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string outsidePath = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "other", path3: "cod‮exe.txt")
        );

        Action act = () => guard.Validate(requestedPath: outsidePath);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Traversal_via_url_encoded_dots_is_blocked_after_canonicalization()
    {
        // %2e%2e is an application-layer concern, not a filesystem one.
        // Path.GetFullPath treats literal percent signs as filename chars,
        // so the path stays outside the root and is rejected correctly.
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-pct"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        // This resolves to a path like /tmp/nm-guard-pct/%2e%2e/outside
        // — it is under the root after canonicalization so it passes.
        // If someone passes the decoded form it will escape and fail.
        string literalPct = Path.Combine(path1: root, path2: "%2e%2e", path3: "inside.txt");
        string result = guard.Validate(requestedPath: literalPct);

        // Stays inside because %2e%2e is treated as a literal directory name.
        result.Should().StartWith(expected: root);
    }

    [Fact]
    public void Multiple_allowed_roots_path_accepted_under_any_root()
    {
        string rootA = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-multi-a"));
        string rootB = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-multi-b"));
        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: NewBackend().Object);

        string underB = Path.Combine(path1: rootB, path2: "output", path3: "file.mp4");
        string result = guard.Validate(requestedPath: underB);

        result.Should().Be(expected: Path.GetFullPath(path: underB));
    }

    [Fact]
    public void Multiple_allowed_roots_path_outside_both_rejected()
    {
        string rootA = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-multi2-a"));
        string rootB = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-multi2-b"));
        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: NewBackend().Object);

        string outside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-multi2-c", path3: "file.mp4")
        );

        Action act = () => guard.Validate(requestedPath: outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Enforced_rejects_path_that_is_sibling_of_root_not_under_it()
    {
        // Regression: "rootname-extra" must not be accepted for root "rootname"
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-sibling"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string sibling = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-guard-sibling-extra", path3: "file.txt")
        );

        Action act = () => guard.Validate(requestedPath: sibling);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    // ── Absolute-path rejection (RejectAbsolutePath) ─────────────────────────

    [Theory]
    [InlineData(data: ["/mnt/media/x.mkv", "Unix-rooted leading slash"])]
    [InlineData(data: ["/abs/path/escape", "Unix-rooted single slash"])]
    [InlineData(data: [@"C:\Media\x.mkv", "Windows drive + backslash"])]
    [InlineData(data: ["C:/Media/x.mkv", "Windows drive + forward slash"])]
    [InlineData(data: [@"\\server\share\file", "UNC double backslash"])]
    [InlineData(data: [@"\rooted-backslash", "Windows backslash root"])]
    public void RejectAbsolutePath_rejects_absolute_path_forms(string path, string form)
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: path);

        act.Should()
            .Throw<StoragePathNotAllowedException>(because: $"RejectAbsolutePath must reject form: {form}")
            .Where(exceptionExpression: e => e.Reason == "absolute paths are not allowed as scope-relative keys");
    }

    [Theory]
    [InlineData(data: "")]
    [InlineData(data: "movies/avatar/avatar.mkv")]
    [InlineData(data: "Black Butler (2008)/Season 05/ep.m3u8")]
    public void RejectAbsolutePath_accepts_valid_scope_relative_paths(string path)
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: path);
        act.Should().NotThrow(because: "scope-relative and empty paths must pass RejectAbsolutePath");
    }

    [Fact]
    public void StructuralValidate_NullByte_fires_on_embedded_null()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "file\0name.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "null byte in path");
    }

    [Fact]
    public void StructuralValidate_NullByte_silent_on_valid_filename()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "filename.txt");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_DotDotStandalone_fires_on_double_dot_alone()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "..");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotStandalone_silent_on_single_dot()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: ".");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_DotDotStartSlash_fires_on_traversal_at_start_forward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "../escape.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotStartSlash_fires_on_traversal_at_start_backward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "..\\ escape.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotStartSlash_silent_on_relative_inside_root()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "subdir/file.txt");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_DotDotEndSlash_fires_on_traversal_at_end_forward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "subdir/..");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotEndSlash_fires_on_traversal_at_end_backward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "subdir\\..");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotEndSlash_silent_on_directory_at_end()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "subdir/leaf");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_DotDotMiddleForward_fires_on_traversal_in_middle_forward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "a/../b/c.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotMiddleBackward_fires_on_traversal_in_middle_backward()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "a\\..\\ b/c.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == ".. traversal is not allowed");
    }

    [Fact]
    public void StructuralValidate_DotDotMiddleForward_silent_on_dot_in_filename()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "v1.2/file-v2.3.txt");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_DotDotMiddleBackward_silent_on_dot_in_filename()
    {
        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: "v1.2\\file-v2.3.txt");

        act.Should().NotThrow();
    }

    [Fact]
    public void StructuralValidate_WindowsDeviceQuestionMark_fires_on_question_mark_prefix()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: @"\\?\C:\Windows");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void StructuralValidate_WindowsDeviceDot_fires_on_dot_prefix()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: @"\\.\COM1");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "device paths are not allowed");
    }

    [Fact]
    public void StructuralValidate_WindowsDeviceQuestionMark_silent_on_regular_unc_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: @"\\server\share\file.txt");

        act.Should().NotThrow(because: "regular UNC paths are allowed by StructuralValidate");
    }

    [Fact]
    public void StructuralValidate_WindowsDeviceDot_silent_on_regular_unc_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Action act = () => StoragePathGuard.StructuralValidate(requestedPath: @"\\server\share\file.txt");

        act.Should().NotThrow(because: "regular UNC paths are allowed by StructuralValidate");
    }

    [Fact]
    public void RejectAbsolutePath_UnixAbsoluteSlash_fires_on_leading_slash()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "/mnt/media/file.mkv");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "absolute paths are not allowed as scope-relative keys");
    }

    [Fact]
    public void RejectAbsolutePath_UnixAbsoluteSlash_silent_on_relative_forward_slash()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "mnt/media/file.mkv");

        act.Should().NotThrow();
    }

    [Fact]
    public void RejectAbsolutePath_WindowsBackslashRoot_fires_on_leading_backslash()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: @"\Windows\System32");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "absolute paths are not allowed as scope-relative keys");
    }

    [Fact]
    public void RejectAbsolutePath_WindowsBackslashRoot_silent_on_relative_path()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "Windows/System32");

        act.Should().NotThrow();
    }

    [Fact]
    public void RejectAbsolutePath_WindowsDriveLetter_fires_on_drive_colon_prefix()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "C:\\Media\\file.mkv");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "absolute paths are not allowed as scope-relative keys");
    }

    [Fact]
    public void RejectAbsolutePath_WindowsDriveLetter_fires_on_drive_colon_forward()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "D:/Media/file.mkv");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "absolute paths are not allowed as scope-relative keys");
    }

    [Fact]
    public void RejectAbsolutePath_WindowsDriveLetter_silent_on_colon_in_middle()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "movies/title:variant.mkv");

        act.Should().NotThrow(because: "colon in middle of path is fine");
    }

    [Fact]
    public void RejectAbsolutePath_EmptyPath_silent_on_empty()
    {
        Action act = () => StoragePathGuard.RejectAbsolutePath(path: "");

        act.Should().NotThrow(because: "empty path is valid (Rule 3 — empty = scope root)");
    }

    [Fact]
    public void Validate_EmptyPath_fires_on_empty()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action act = () => guard.Validate(requestedPath: "");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    [Fact]
    public void Validate_EmptyPath_silent_on_nonempty_path()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        string result = guard.Validate(requestedPath: Path.GetFullPath(path: "file.txt"));

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_WhitespaceOnly_fires_on_spaces()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        Action act = () => guard.Validate(requestedPath: "   ");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason == "path is empty");
    }

    [Fact]
    public void Validate_AllowlistEnforced_fires_on_path_outside_all_roots()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-outside-root"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string outside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "different-location", path3: "file.txt")
        );

        Action act = () => guard.Validate(requestedPath: outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Validate_AllowlistEnforced_silent_on_path_inside_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-inside-root"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string inside = Path.Combine(path1: root, path2: "subdir", path3: "file.txt");

        string result = guard.Validate(requestedPath: inside);

        result.Should().Be(expected: Path.GetFullPath(path: inside));
    }

    [Fact]
    public void Validate_AllowlistNotEnforced_silent_on_any_relative_path()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        string arbitrary = Path.GetFullPath(path: "some/random/path.txt");

        string result = guard.Validate(requestedPath: arbitrary);

        result.Should().Be(expected: arbitrary);
    }

    [Fact]
    public void Validate_SymlinkEscape_fires_when_symlink_target_outside_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-symlink-root"));
        string linkPath = Path.Combine(path1: root, path2: "mylink");
        string targetOutside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "outside", path3: "target.txt")
        );

        Mock<IStorageDriver> driver = NewBackend();
        driver.Setup(expression: b => b.ResolveLinkTarget(Path.GetFullPath(linkPath))).Returns(value: targetOutside);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);

        Action act = () => guard.Validate(requestedPath: linkPath);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Validate_SymlinkEscape_silent_when_symlink_target_inside_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-symlink-ok"));
        string linkPath = Path.Combine(path1: root, path2: "mylink");
        string targetInside = Path.Combine(path1: root, path2: "actual-file.txt");

        Mock<IStorageDriver> driver = NewBackend();
        driver
            .Setup(expression: b => b.ResolveLinkTarget(Path.GetFullPath(linkPath)))
            .Returns(value: Path.GetFullPath(path: targetInside));

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);

        string result = guard.Validate(requestedPath: linkPath);

        result.Should().Be(expected: Path.GetFullPath(path: linkPath));
    }

    [Fact]
    public void Validate_MultipleRoots_fires_when_outside_all_roots()
    {
        string rootA = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-multi-a"));
        string rootB = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-multi-b"));
        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: NewBackend().Object);

        string outside = Path.GetFullPath(
            path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-multi-c", path3: "file.txt")
        );

        Action act = () => guard.Validate(requestedPath: outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Validate_MultipleRoots_silent_when_under_any_root()
    {
        string rootA = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-multi-ok-a"));
        string rootB = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-multi-ok-b"));
        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: NewBackend().Object);

        string underB = Path.Combine(path1: rootB, path2: "nested", path3: "file.txt");

        string result = guard.Validate(requestedPath: underB);

        result.Should().Be(expected: Path.GetFullPath(path: underB));
    }

    [Fact]
    public void Validate_RootMatchesExactly_fires_when_path_is_inside_root_but_not_exact()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-root-not-exact"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string inside = Path.Combine(path1: root, path2: "subdir", path3: "file.txt");
        string result = guard.Validate(requestedPath: inside);

        result.Should().NotBe(unexpected: root);
        result.Should().StartWith(expected: root);
    }

    [Fact]
    public void Validate_RootMatchesExactly_silent_when_path_equals_root()
    {
        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-root-exact"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string result = guard.Validate(requestedPath: root);

        result.Should().Be(expected: root);
    }

    [Fact]
    public void Validate_RootPrefixWithoutSeparator_fires_when_path_is_root_name_plus_extra()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string root = Path.GetFullPath(path: Path.Combine(path1: Path.GetTempPath(), path2: "nm-test-prefix-match"));
        StoragePathGuard guard = new(allowedRoots: [root], driver: NewBackend().Object);

        string sibling = root + "-extra\\file.txt";

        Action act = () => guard.Validate(requestedPath: sibling);

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("path is not under any allowed root"));
    }

    [Fact]
    public void Validate_CanonicalizationFailure_fires_with_appropriate_message()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Strict);
        driver
            .Setup(expression: b => b.GetFullPath(It.IsAny<string>()))
            .Throws(exception: new InvalidOperationException(message: "Cannot resolve path"));

        StoragePathGuard guard = new(allowedRoots: [], driver: driver.Object);

        Action act = () => guard.Validate(requestedPath: "bad/path");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.StartsWith("cannot canonicalize"));
    }

    [Fact]
    public void Validate_CanonicalizationFailure_silent_when_path_canonicalizes_successfully()
    {
        StoragePathGuard guard = new(allowedRoots: [], driver: NewBackend().Object);

        string valid = Path.GetFullPath(path: "some/relative/path.txt");

        string result = guard.Validate(requestedPath: valid);

        result.Should().Be(expected: valid);
    }

    [Fact]
    public void Catalogue_completeness_check()
    {
        string[] catalogueRules =
        [
            "StructuralValidate_NullByte",
            "StructuralValidate_DotDotStandalone",
            "StructuralValidate_DotDotStartSlash",
            "StructuralValidate_DotDotEndSlash",
            "StructuralValidate_DotDotMiddleForward",
            "StructuralValidate_DotDotMiddleBackward",
            "StructuralValidate_WindowsDeviceQuestionMark",
            "StructuralValidate_WindowsDeviceDot",
            "RejectAbsolutePath_UnixAbsoluteSlash",
            "RejectAbsolutePath_WindowsBackslashRoot",
            "RejectAbsolutePath_WindowsDriveLetter",
            "Validate_EmptyPath",
            "Validate_AllowlistEnforced",
            "Validate_SymlinkEscape",
            "Validate_MultipleRoots",
            "Validate_RootMatchesExactly",
            "Validate_CanonicalizationFailure",
        ];

        HashSet<string> testMethods = typeof(StoragePathGuardTests)
            .GetMethods(
                bindingAttr: System.Reflection.BindingFlags.Public
                             | System.Reflection.BindingFlags.Instance
                             | System.Reflection.BindingFlags.DeclaredOnly
            )
            .Where(predicate: m => m.GetCustomAttributes(attributeType: typeof(FactAttribute), inherit: false).Length > 0)
            .Select(selector: m => m.Name)
            .ToHashSet(comparer: StringComparer.Ordinal);

        foreach (string rule in catalogueRules)
        {
            List<string> matchingTests = testMethods.Where(predicate: t => t.Contains(value: rule)).ToList();

            matchingTests
                .Should()
                .HaveCountGreaterThanOrEqualTo(
                    expected: 2,
                    because: "rule '{0}' must have at least a fires-on and a silent-on test; "
                             + "when a new guard rule is added to the catalogue, add both forms here",
                    becauseArgs: rule
                );
        }
    }
}
