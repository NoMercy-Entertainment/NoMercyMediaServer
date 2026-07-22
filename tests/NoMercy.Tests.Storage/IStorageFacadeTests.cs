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
/// Tests the IStorage/IStorageDriver facade contract: path validation, root
/// resolution, safe-path acceptance, and malicious/traversal rejection.
/// Fixtures seed both valid and decoy/invalid paths to prove the validation
/// is actually exercised — not just that paths exist.
/// </summary>
public class IStorageFacadeTests
{
    private static Mock<IStorageDriver> NewDriver()
    {
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        driver
            .Setup(expression: d => d.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => Path.GetFullPath(path: p));
        driver.Setup(expression: d => d.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
        driver.Setup(expression: d => d.DirectorySeparator).Returns(value: '/');
        // CombinePath has a default implementation in the interface, so Moq will call it
        driver
            .Setup(expression: d => d.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(
                valueFunction: (parent, child) =>
                {
                    if (string.IsNullOrEmpty(value: child))
                        return parent;
                    if (string.IsNullOrEmpty(value: parent))
                        return child;
                    string trimmedParent = parent.TrimEnd(trimChars: ['/', '\\']);
                    string trimmedChild = child.TrimStart(trimChars: ['/', '\\']);
                    return $"{trimmedParent}/{trimmedChild}";
                }
            );
        return driver;
    }

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 1 (scope-relative) + Rule 2 (forward slashes)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateScoped_normalizes_backslashes_to_forward_slashes()
    {
        // Regression: Windows Path.Combine produces backslashes; IStorage
        // normalizes them internally so the scope-relative result is
        // separator-agnostic (Rule 2).
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-storage-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        // Windows Path.Combine produces "a\b\c.txt"; input to storage should accept both
        string withBackslashes = @"subdir\file.txt";
        string withForwardSlashes = "subdir/file.txt";
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: true);

        // Both forms should be accepted and normalized consistently
        bool existsBackslash = storage.Exists(path: withBackslashes);
        bool existsForwardSlash = storage.Exists(path: withForwardSlashes);

        existsBackslash.Should().Be(expected: true, because: "backslashes should be tolerated and normalized");
        existsForwardSlash.Should().Be(expected: true, because: "forward slashes should work directly");
        driver.Verify(
            expression: d => d.FileExists(It.IsAny<string>()),
            times: Times.Exactly(callCount: 2),
            failMessage: "both normalized paths should hit the driver"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 3 (empty path = root)
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void List_with_empty_path_resolves_to_scoped_root()
    {
        // Empty string means "list the scope root", not "throw path is empty".
        // This is necessary for dashboard browsing from the root.
        // This test is a copy of LocalStorageUnitTests pattern, ensuring the contract holds.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-list-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: root);
        try
        {
            Mock<IStorageDriver> driver = NewDriver();
            driver
                .Setup(expression: d =>
                    d.EnumerateEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns(value: []);
            driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);

            StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
            IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

            // Empty path should not throw; instead it should resolve to the root
            IReadOnlyList<StorageEntry> result = storage.List(path: "", pattern: null, recursive: false);

            result.Should().NotBeNull(because: "empty path should not throw");
            driver.Verify(
                expression: d =>
                    d.EnumerateEntries(
                        It.Is<string>(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    ),
                times: Times.AtLeastOnce(),
                failMessage: "empty path must resolve to root for enumeration"
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

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 4 (absolute paths rejected)
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "/etc/passwd")]
    [InlineData(data: @"C:\Windows\System32")]
    [InlineData(data: @"\\server\share\file")]
    public async Task ReadAsync_rejects_absolute_paths_before_driver_sees_them(string absolutePath)
    {
        // Absolute paths indicate the caller bypassed the abstraction.
        // Remote drivers reject these explicitly via RejectAbsolutePath.
        // Local storage still rejects them via the guard's under-root check.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-abs-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Func<Task> act = async () => await storage.ReadAsync(path: absolutePath, ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                because: "absolute paths must be rejected by the guard"
            );
        driver.Verify(
            expression: d => d.OpenRead(It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for absolute paths"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 5 (..) traversal rejection
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: "..")]
    [InlineData(data: "../escape")]
    [InlineData(data: "a/../..")]
    [InlineData(data: "subdir/../../etc/passwd")]
    public void Exists_rejects_traversal_sequences_before_driver_call(string traversalPath)
    {
        // ".." traversal is a fundamental security violation.
        // Structural validation catches it BEFORE any backend call.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-trav-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Action act = () => storage.Exists(path: traversalPath);

        act.Should()
            .Throw<StoragePathNotAllowedException>(
                because: "traversal sequences must be rejected structurally"
            )
            .Where(exceptionExpression: e => e.Reason.Contains("traversal"));
        driver.Verify(
            expression: d => d.FileExists(It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for traversal paths"
        );
    }

    [Fact]
    public void ValidateScoped_rejects_null_bytes_in_path()
    {
        // Null bytes are a classic filesystem attack vector.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-null-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Action act = () => storage.Exists(path: "file\0name.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(exceptionExpression: e => e.Reason.Contains("null byte"));
        driver.Verify(
            expression: d => d.FileExists(It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for paths with null bytes"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Root resolution: allowed roots vs. paths under them
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Enforced_guard_accepts_path_under_root_rejects_outside()
    {
        // Discriminating fixture: seed one path INSIDE the root, one OUTSIDE,
        // prove only the inside one is accepted and actually read.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-enforce-{Guid.NewGuid():N}");
        string insidePath = "media/movies/avatar.mkv";
        string outsidePath = "../../../etc/passwd";

        Mock<IStorageDriver> driver = NewDriver();
        // Match on full paths resolved against the root
        driver
            .Setup(expression: d => d.FileExists(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => p.Contains(value: "avatar"));
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        // Inside should succeed
        bool insideExists = storage.Exists(path: insidePath);
        insideExists.Should().BeTrue(because: "path under root must be accepted");

        // Outside should fail
        Action actOutside = () => storage.Exists(path: outsidePath);
        actOutside
            .Should()
            .Throw<StoragePathNotAllowedException>(because: "traversal outside root must be rejected");

        driver.Verify(
            expression: d => d.FileExists(It.IsAny<string>()),
            times: Times.Once,
            failMessage: "driver must be called for inside path"
        );
    }

    [Fact]
    public void Multiple_allowed_roots_path_accepted_under_any_root()
    {
        // A single path may be under any of several configured roots.
        string rootA = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-root-a-{Guid.NewGuid():N}");
        string rootB = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-root-b-{Guid.NewGuid():N}");

        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: true);

        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: driver.Object);
        LocalStorage storage = new(driver: driver.Object, guard: guard);

        // This relative path will be resolved under root A (first root in the list)
        bool exists = storage.Exists(path: "file.txt");

        exists.Should().BeTrue(because: "path under any allowed root must be accepted");
        driver.Verify(expression: d => d.FileExists(It.IsAny<string>()), times: Times.Once);
    }

    [Fact]
    public void Multiple_allowed_roots_path_outside_all_rejected()
    {
        // A path outside ALL configured roots must be rejected.
        string rootA = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-multi-a-{Guid.NewGuid():N}");
        string rootB = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-multi-b-{Guid.NewGuid():N}");
        string outside = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-outside-{Guid.NewGuid():N}");

        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [rootA, rootB], driver: driver.Object);
        LocalStorage storage = new(driver: driver.Object, guard: guard);

        Action act = () => storage.Exists(path: outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>(because: "path outside all roots must be rejected");
        driver.Verify(
            expression: d => d.FileExists(It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for paths outside all roots"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Symlink escapes
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Enforced_guard_rejects_symlink_target_escaping_root()
    {
        // A symlink inside the root may point outside it.
        // The guard canonicalizes and checks the REAL target.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-link-{Guid.NewGuid():N}");
        string linkPath = Path.Combine(path1: root, path2: "escape-link");
        string realTarget = Path.Combine(path1: Path.GetTempPath(), path2: "outside", path3: "real.txt");

        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(expression: d => d.GetFullPath(It.IsAny<string>()))
            .Returns<string>(valueFunction: p =>
            {
                // Simulate symlink resolution: the link path resolves to the outside target
                if (p.Contains(value: "escape-link"))
                    return realTarget;
                return Path.GetFullPath(path: p);
            });
        driver
            .Setup(expression: d => d.ResolveLinkTarget(It.Is<string>(p => p.Contains("escape-link"))))
            .Returns(value: realTarget);
        driver.Setup(expression: d => d.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);

        Action act = () => guard.Validate(requestedPath: "escape-link");

        act.Should()
            .Throw<StoragePathNotAllowedException>(because: "symlinks escaping the root must be rejected");
    }

    // ────────────────────────────────────────────────────────────────────
    // CombinePath: storage-aware path construction
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void CombinePath_uses_driver_separator_not_os_separator()
    {
        // On Windows, Path.Combine produces backslashes.
        // IStorage.CombinePath must use the driver's separator.
        // This is critical for remote drivers that speak '/'.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-combine-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectorySeparator).Returns(value: '/');
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string combined = storage.CombinePath(parent: "movies", child: "avatar/2009");

        combined
            .Should()
            .NotContain(unexpected: "\\", because: "CombinePath must not produce OS backslashes on Windows");
        combined
            .Should()
            .Be(expected: "movies/avatar/2009", because: "CombinePath must use forward slashes for storage paths");
    }

    [Fact]
    public void CombinePath_trims_redundant_separators()
    {
        // Inputs like "parent/" + "/child" should produce "parent/child", not "parent//child".
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-trim-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectorySeparator).Returns(value: '/');
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string combined = storage.CombinePath(parent: "parent/", child: "/child");

        combined.Should().Be(expected: "parent/child", because: "redundant separators must be trimmed");
    }

    [Fact]
    public void CombinePath_handles_empty_parent_or_child()
    {
        // Empty parent: result is child.
        // Empty child: result is parent.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-empty-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectorySeparator).Returns(value: '/');
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string emptyParent = storage.CombinePath(parent: "", child: "child");
        string emptyChild = storage.CombinePath(parent: "parent", child: "");

        emptyParent.Should().Be(expected: "child", because: "empty parent should return child");
        emptyChild.Should().Be(expected: "parent", because: "empty child should return parent");
    }

    // ────────────────────────────────────────────────────────────────────
    // GetName, GetParent, GetNameWithoutExtension (scope-relative operations)
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(data: ["file.txt", "file.txt"])]
    [InlineData(data: ["dir/file.txt", "file.txt"])]
    [InlineData(data: ["a/b/c/d.mkv", "d.mkv"])]
    [InlineData(data: ["a/b/c/d.mkv/", "d.mkv"])] // trailing slash should be ignored
    public void GetName_returns_last_segment_of_path(string path, string expectedName)
    {
        // GetName must work on scope-relative paths without touching the filesystem.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-getname-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string name = storage.GetName(path: path);

        name.Should().Be(expected: expectedName, because: "GetName must return the last path segment");
    }

    [Theory]
    [InlineData(data: ["file.txt", null])]
    [InlineData(data: ["dir/file.txt", "dir"])]
    [InlineData(data: ["a/b/c/d.mkv", "a/b/c"])]
    [InlineData(data: ["", null])]
    public void GetParent_returns_parent_directory_segment(string path, string? expectedParent)
    {
        // GetParent returns the directory containing the path,
        // or null if the path is already at the scope root.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-parent-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string? parent = storage.GetParent(path: path);

        parent
            .Should()
            .Be(expected: expectedParent, because: "GetParent must return the directory or null for root entries");
    }

    [Theory]
    [InlineData(data: ["file.txt", "file"])]
    [InlineData(data: ["archive.tar.gz", "archive.tar"])]
    [InlineData(data: ["dir/noext", "noext"])]
    [InlineData(data: ["dir/multi.dot.txt", "multi.dot"])]
    public void GetNameWithoutExtension_strips_extension_from_name(string path, string expectedName)
    {
        // This is GetName + extension stripping: get the last segment, then strip its extension.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-namenoext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string name = storage.GetNameWithoutExtension(path: path);

        name.Should()
            .Be(
                expected: expectedName,
                because: "GetNameWithoutExtension must return the last segment without its extension"
            );
    }

    // ────────────────────────────────────────────────────────────────────
    // Write operations: parent directory creation
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_creates_parent_directories_automatically()
    {
        // IStorage.WriteAsync guarantees parent dirs are created.
        // This is critical for encoder output and library scanning.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-write-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        MemoryStream sink = new();
        driver.Setup(expression: d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(value: sink);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        await storage.WriteAsync(
            path: "deep/nested/output/file.mp4",
            bytes: new byte[] { 0xAA, 0xBB },
            ct: CancellationToken.None
        );

        driver.Verify(
            expression: d => d.CreateDirectory(It.IsAny<string>()),
            times: Times.AtLeastOnce(),
            failMessage: "parent directories must be created before write"
        );
        sink.ToArray()
            .Should()
            .Equal(expected: new byte[] { 0xAA, 0xBB }, because: "payload must be written to the stream");
    }

    [Fact]
    public async Task OpenWriteAsync_creates_parent_directories_before_opening_stream()
    {
        // Same guarantee as WriteAsync: parents exist before the stream is returned.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-openw-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        MemoryStream stream = new();
        driver.Setup(expression: d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(value: stream);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Stream result = await storage.OpenWriteAsync(
            path: "dir/subdir/file.mp4",
            overwrite: true,
            ct: CancellationToken.None
        );

        driver.Verify(
            expression: d => d.CreateDirectory(It.IsAny<string>()),
            times: Times.AtLeastOnce(),
            failMessage: "parent directories must exist before OpenWrite returns"
        );
        result.Should().BeSameAs(expected: stream, because: "the driver's stream must be returned, not wrapped");
    }

    // ────────────────────────────────────────────────────────────────────
    // Move and Copy: both paths validated, parents ensured
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveAsync_validates_source_and_destination_independently()
    {
        // Discriminating fixture: valid source, invalid dest (traversal).
        // Prove the validation catches the bad dest before the move.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-move-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Func<Task> act = async () =>
            await storage.MoveAsync(from: "valid/source.txt", to: "../../../escape", ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                because: "bad destination path must be rejected even if source is valid"
            );
        driver.Verify(
            expression: d => d.MoveFile(It.IsAny<string>(), It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for paths with validation errors"
        );
    }

    [Fact]
    public async Task CopyAsync_creates_parent_directory_for_destination()
    {
        // CopyAsync must ensure the destination's parent exists.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-copy-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        driver
            .Setup(expression: d => d.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback(action: () => { }); // no-op

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        await storage.CopyAsync(from: "source.txt", to: "deep/nested/dest.txt", ct: CancellationToken.None);

        driver.Verify(
            expression: d => d.CreateDirectory(It.IsAny<string>()),
            times: Times.AtLeastOnce(),
            failMessage: "destination parent directories must be created"
        );
        driver.Verify(
            expression: d => d.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            times: Times.Once(),
            failMessage: "copy must be called after parents are ensured"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // List operations with filtering and scope-relative return paths
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void List_returns_scope_relative_paths_not_driver_paths()
    {
        // Critical contract: List() returns paths suitable for passing
        // back into IStorage methods (Rule 1). Callers must not join
        // these with OS paths or pass them to System.IO APIs.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-list-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        // Driver returns OS-absolute paths (its contract)
        string absoluteFile = Path.Combine(path1: root, path2: "subdir", path3: "file.txt");
        string absoluteDir = Path.Combine(path1: root, path2: "subdir", path3: "childdir");

        driver
            .Setup(expression: d =>
                d.EnumerateEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns(value:
            [
                new StorageEntryInfo(Path: absoluteFile, IsDirectory: false, Size: 42L, LastWriteUtc: DateTime.UtcNow),
                new StorageEntryInfo(Path: absoluteDir, IsDirectory: true, Size: 0L, LastWriteUtc: DateTime.UtcNow),
            ]);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        IReadOnlyList<StorageEntry> result = storage.List(path: "", pattern: null, recursive: false);

        result.Should().HaveCount(expected: 2, because: "all entries must be returned");
        result[index: 0].Path.Should().NotStartWith(unexpected: "/", because: "scope-relative paths must not start with /");
        result[index: 0].Path.Should().NotStartWith(unexpected: "\\", because: "scope-relative paths must not start with \\");
        result[index: 0]
            .Path.Should()
            .NotStartWith(unexpected: root, because: "scope-relative paths must not include the root prefix");
    }

    [Fact]
    public void List_applies_pattern_filter_via_driver()
    {
        // The pattern parameter is passed to the driver's enumeration.
        // Only matching files should be returned.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-pattern-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(expression: d =>
                d.EnumerateEntries(It.IsAny<string>(), "*.mkv", SearchOption.TopDirectoryOnly)
            )
            .Returns(value:
            [
                new StorageEntryInfo(
                    Path: Path.Combine(path1: root, path2: "movie.mkv"),
                    IsDirectory: false,
                    Size: 1024L,
                    LastWriteUtc: DateTime.UtcNow
                ),
            ]);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        IReadOnlyList<StorageEntry> result = storage.List(path: "", pattern: "*.mkv", recursive: false);

        result.Should().ContainSingle(because: "pattern filter must be applied");
        result[index: 0].Path.Should().EndWith(expected: ".mkv", because: "only matching files must be returned");
        driver.Verify(
            expression: d => d.EnumerateEntries(It.IsAny<string>(), "*.mkv", SearchOption.TopDirectoryOnly),
            times: Times.Once(),
            failMessage: "pattern must be passed to the driver"
        );
    }

    [Fact]
    public void List_recursive_true_passes_AllDirectories_to_driver()
    {
        // Recursive=true should traverse the entire subtree.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-recurse-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(expression: d =>
                d.EnumerateEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    SearchOption.AllDirectories
                )
            )
            .Returns(value:
            [
                new StorageEntryInfo(Path: Path.Combine(path1: root, path2: "a.txt"), IsDirectory: false, Size: 100L, LastWriteUtc: DateTime.UtcNow),
                new StorageEntryInfo(
                    Path: Path.Combine(path1: root, path2: "deep", path3: "b.txt"),
                    IsDirectory: false,
                    Size: 100L,
                    LastWriteUtc: DateTime.UtcNow
                ),
                new StorageEntryInfo(
                    Path: Path.Combine(path1: root, path2: "deep", path3: "deeper", path4: "c.txt"),
                    IsDirectory: false,
                    Size: 100L,
                    LastWriteUtc: DateTime.UtcNow
                ),
            ]);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        IReadOnlyList<StorageEntry> result = storage.List(path: "", pattern: null, recursive: true);

        result.Should().HaveCount(expected: 3, because: "all files in the tree must be returned");
        driver.Verify(
            expression: d =>
                d.EnumerateEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    SearchOption.AllDirectories
                ),
            times: Times.Once(),
            failMessage: "recursive=true must pass AllDirectories to the driver"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Existence checks: files vs. directories
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Exists_returns_true_for_file()
    {
        // Exists must return true for both files and directories (Rule 1).
        // Discriminating fixture: file exists, directory doesn't.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-exists-file-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(expression: d => d.FileExists(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => p.EndsWith(value: "file.txt"));
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        bool exists = storage.Exists(path: "file.txt");

        exists.Should().BeTrue(because: "existing files must return true");
        driver.Verify(expression: d => d.FileExists(It.IsAny<string>()), times: Times.Once());
    }

    [Fact]
    public void Exists_returns_true_for_directory()
    {
        // Exists must also return true for directories.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-exists-dir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);
        driver
            .Setup(expression: d => d.DirectoryExists(It.IsAny<string>()))
            .Returns<string>(valueFunction: p => p.EndsWith(value: "somedir"));

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        bool exists = storage.Exists(path: "somedir");

        exists.Should().BeTrue(because: "existing directories must return true");
        driver.Verify(expression: d => d.DirectoryExists(It.IsAny<string>()), times: Times.Once());
    }

    [Fact]
    public void Exists_returns_false_when_neither_file_nor_directory()
    {
        // Exists returns false only when both checks fail.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-notexists-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        bool exists = storage.Exists(path: "nonexistent.txt");

        exists.Should().BeFalse(because: "nonexistent paths must return false");
    }

    // ────────────────────────────────────────────────────────────────────
    // SizeOrZero: zero for missing, size for existing
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void SizeOrZero_returns_size_when_file_exists()
    {
        // SizeOrZero avoids the (await Exists) ? (await Size) : 0 pattern.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-sizeorz-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: true);
        driver.Setup(expression: d => d.GetFileSize(It.IsAny<string>())).Returns(value: 12345L);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        long size = storage.SizeOrZero(path: "file.mp4");

        size.Should().Be(expected: 12345L, because: "size must be returned for existing files");
    }

    [Fact]
    public void SizeOrZero_returns_zero_when_file_missing()
    {
        // Missing files return 0, not an exception.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-size0-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        long size = storage.SizeOrZero(path: "missing.mp4");

        size.Should().Be(expected: 0L, because: "missing files must return 0, not throw");
    }

    // ────────────────────────────────────────────────────────────────────
    // Delete operations: file vs. directory
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_is_no_op_when_file_missing()
    {
        // Delete must be idempotent: deleting a nonexistent file is safe.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-del-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        storage.Delete(path: "nonexistent.txt");

        driver.Verify(
            expression: d => d.DeleteFile(It.IsAny<string>()),
            times: Times.Never,
            failMessage: "delete must not call driver for missing files"
        );
    }

    [Fact]
    public void Delete_calls_driver_when_file_present()
    {
        // Delete must delegate to the driver when the file exists.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-delfile-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: true);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        storage.Delete(path: "exists.txt");

        driver.Verify(expression: d => d.DeleteFile(It.IsAny<string>()), times: Times.Once());
    }

    [Fact]
    public void DeleteDirectory_with_recursive_true_clears_subtree()
    {
        // DeleteDirectory(path, recursive: true) must remove the directory
        // and all its contents.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-deldir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        storage.DeleteDirectory(path: "old-library", recursive: true);

        driver.Verify(
            expression: d => d.DeleteDirectory(It.IsAny<string>(), true),
            times: Times.Once(),
            failMessage: "recursive deletion must pass true to the driver"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Create directory: idempotent
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateDirectory_delegates_to_driver()
    {
        // CreateDirectory must call the driver's CreateDirectory method.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-mkdir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        storage.CreateDirectory(path: "new/nested/dir");

        driver.Verify(expression: d => d.CreateDirectory(It.IsAny<string>()), times: Times.Once());
    }

    // ────────────────────────────────────────────────────────────────────
    // DirectorySeparator and Driver property
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectorySeparator_returns_driver_separator()
    {
        // LocalStorage's separator is the driver's; remote drivers use '/'.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-sep-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectorySeparator).Returns(value: '/');

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        char sep = storage.DirectorySeparator;

        sep.Should().Be(expected: '/', because: "separator must come from the driver");
    }

    [Fact]
    public void Driver_property_exposes_underlying_driver()
    {
        // IStorage.Driver allows consumers to access the low-level backend
        // when they need it (e.g., MediaScan).
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-driver-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        IStorageDriver result = storage.Driver;

        result.Should().BeSameAs(expected: driver.Object, because: "Driver property must expose the driver");
    }

    // ────────────────────────────────────────────────────────────────────
    // Hash: algorithm validation, lowercase hex output
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HashAsync_supports_sha256()
    {
        // Hash must support "sha256" (case-insensitive).
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-hash-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        byte[] fileData = [0x48, 0x65, 0x6C, 0x6C, 0x6F]; // "Hello"
        driver.Setup(expression: d => d.OpenRead(It.IsAny<string>())).Returns(value: new MemoryStream(buffer: fileData));

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string hash = await storage.HashAsync(path: "file.txt", algorithm: "sha256", ct: CancellationToken.None);

        hash.Should().NotBeNullOrEmpty(because: "hash must be computed");
        hash.Should().Be(expected: hash.ToLowerInvariant(), because: "hash must be lowercase hex");
        hash.Length.Should().Be(expected: 64, because: "SHA256 produces 32 bytes = 64 hex chars");
    }

    [Fact]
    public async Task HashAsync_supports_md5()
    {
        // Hash must also support "md5".
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-md5-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.OpenRead(It.IsAny<string>())).Returns(value: new MemoryStream(buffer: [0x61])); // "a"

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string hash = await storage.HashAsync(path: "file.txt", algorithm: "md5", ct: CancellationToken.None);

        hash.Should().NotBeNullOrEmpty(because: "md5 hash must be computed");
        hash.Length.Should().Be(expected: 32, because: "MD5 produces 16 bytes = 32 hex chars");
    }

    [Fact]
    public async Task HashAsync_rejects_unsupported_algorithm()
    {
        // Only sha256 and md5 are supported.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-nosuch-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Func<Task> act = async () =>
            await storage.HashAsync(path: "file.txt", algorithm: "blake2b", ct: CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>(because: "unsupported algorithms must be rejected");
    }

    // ────────────────────────────────────────────────────────────────────
    // AcquireLocalPath: lease for child process consumption
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireLocalPathAsync_returns_absolute_path()
    {
        // AcquireLocalPath returns an OS-absolute path for child process use.
        // This path is NOT scope-relative (it escapes the abstraction by design).
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-lease-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        LocalPathLease lease = await storage.AcquireLocalPathAsync(
            path: "media/video.mp4",
            ct: CancellationToken.None
        );

        lease.Path.Should().NotBeNullOrEmpty(because: "lease must contain a path");
        Path.IsPathRooted(path: lease.Path)
            .Should()
            .BeTrue(because: "lease path must be OS-absolute for child process use");
        lease.Dispose();
    }

    // ────────────────────────────────────────────────────────────────────
    // ReadAllTextAsync / WriteAllTextAsync: text content operations
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAllTextAsync_writes_string_content()
    {
        // WriteAllTextAsync is a convenience for text content.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-writetext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);
        MemoryStream sink = new();
        driver.Setup(expression: d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(value: sink);

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        await storage.WriteAllTextAsync(
            path: "config.json",
            contents: "{\"key\": \"value\"}",
            ct: CancellationToken.None
        );

        sink.ToArray().Should().Equal(elements: System.Text.Encoding.UTF8.GetBytes(s: "{\"key\": \"value\"}"));
    }

    [Fact]
    public async Task ReadAllTextAsync_reads_text_content()
    {
        // ReadAllTextAsync decodes bytes to a string.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-readtext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        string content = "line1\nline2\nline3";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s: content);
        driver.Setup(expression: d => d.OpenRead(It.IsAny<string>())).Returns(value: new MemoryStream(buffer: bytes));

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        string result = await storage.ReadAllTextAsync(path: "file.txt", ct: CancellationToken.None);

        result.Should().Be(expected: content, because: "text content must be read correctly");
    }

    // ────────────────────────────────────────────────────────────────────
    // MoveDirectory / MoveDirectoryAsync
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveDirectoryAsync_validates_both_paths()
    {
        // MoveDirectory must validate both source and destination paths.
        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-movedir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        // Moving to a path with traversal should fail validation
        Func<Task> act = async () =>
            await storage.MoveDirectoryAsync(from: "old", to: "../../../escape", ct: CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(because: "bad destination path must be rejected");
        driver.Verify(
            expression: d => d.MoveDirectory(It.IsAny<string>(), It.IsAny<string>()),
            times: Times.Never,
            failMessage: "driver must not be called for invalid paths"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Rejection of Windows device paths
    // ────────────────────────────────────────────────────────────────────

    [SkippableFact]
    public void Reject_device_paths_on_windows()
    {
        Skip.IfNot(condition: OperatingSystem.IsWindows(), reason: "Device paths are Windows-specific");

        string root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-devpath-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new(allowedRoots: [root], driver: driver.Object);
        IStorage storage = new LocalStorage(driver: driver.Object, guard: guard);

        Action act = () => storage.Exists(path: @"\\?\C:\Windows");

        act.Should()
            .Throw<StoragePathNotAllowedException>(because: "device paths must be rejected on Windows");
    }
}
