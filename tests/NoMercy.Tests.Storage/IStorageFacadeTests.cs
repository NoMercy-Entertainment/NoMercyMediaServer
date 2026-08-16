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
        Mock<IStorageDriver> driver = new(MockBehavior.Loose);
        driver
            .Setup(d => d.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p => Path.GetFullPath(p));
        driver.Setup(d => d.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);
        driver.Setup(d => d.DirectorySeparator).Returns('/');
        // CombinePath has a default implementation in the interface, so Moq will call it
        driver
            .Setup(d => d.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>(
                (parent, child) =>
                {
                    if (string.IsNullOrEmpty(child))
                        return parent;
                    if (string.IsNullOrEmpty(parent))
                        return child;
                    string trimmedParent = parent.TrimEnd('/', '\\');
                    string trimmedChild = child.TrimStart('/', '\\');
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-storage-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        // Windows Path.Combine produces "a\b\c.txt"; input to storage should accept both
        string withBackslashes = @"subdir\file.txt";
        string withForwardSlashes = "subdir/file.txt";
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);

        // Both forms should be accepted and normalized consistently
        bool existsBackslash = storage.Exists(withBackslashes);
        bool existsForwardSlash = storage.Exists(withForwardSlashes);

        existsBackslash.Should().Be(true, "backslashes should be tolerated and normalized");
        existsForwardSlash.Should().Be(true, "forward slashes should work directly");
        driver.Verify(
            d => d.FileExists(It.IsAny<string>()),
            Times.Exactly(2),
            "both normalized paths should hit the driver"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-list-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Mock<IStorageDriver> driver = NewDriver();
            driver
                .Setup(d =>
                    d.EnumerateEntries(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    )
                )
                .Returns([]);
            driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(true);

            StoragePathGuard guard = new([root], driver.Object);
            IStorage storage = new LocalStorage(driver.Object, guard);

            // Empty path should not throw; instead it should resolve to the root
            IReadOnlyList<StorageEntry> result = storage.List("", null, recursive: false);

            result.Should().NotBeNull("empty path should not throw");
            driver.Verify(
                d =>
                    d.EnumerateEntries(
                        It.Is<string>(p => p.StartsWith(root, StringComparison.OrdinalIgnoreCase)),
                        It.IsAny<string>(),
                        It.IsAny<SearchOption>()
                    ),
                Times.AtLeastOnce(),
                "empty path must resolve to root for enumeration"
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

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 4 (absolute paths rejected)
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"\\server\share\file")]
    public async Task ReadAsync_rejects_absolute_paths_before_driver_sees_them(string absolutePath)
    {
        // Absolute paths indicate the caller bypassed the abstraction.
        // Remote drivers reject these explicitly via RejectAbsolutePath.
        // Local storage still rejects them via the guard's under-root check.
        string root = Path.Combine(Path.GetTempPath(), $"nm-abs-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Func<Task> act = async () => await storage.ReadAsync(absolutePath, CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                "absolute paths must be rejected by the guard"
            );
        driver.Verify(
            d => d.OpenRead(It.IsAny<string>()),
            Times.Never,
            "driver must not be called for absolute paths"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Path Contract Rule 5 (..) traversal rejection
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("a/../..")]
    [InlineData("subdir/../../etc/passwd")]
    public void Exists_rejects_traversal_sequences_before_driver_call(string traversalPath)
    {
        // ".." traversal is a fundamental security violation.
        // Structural validation catches it BEFORE any backend call.
        string root = Path.Combine(Path.GetTempPath(), $"nm-trav-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Action act = () => storage.Exists(traversalPath);

        act.Should()
            .Throw<StoragePathNotAllowedException>(
                "traversal sequences must be rejected structurally"
            )
            .Where(e => e.Reason.Contains("traversal"));
        driver.Verify(
            d => d.FileExists(It.IsAny<string>()),
            Times.Never,
            "driver must not be called for traversal paths"
        );
    }

    [Fact]
    public void ValidateScoped_rejects_null_bytes_in_path()
    {
        // Null bytes are a classic filesystem attack vector.
        string root = Path.Combine(Path.GetTempPath(), $"nm-null-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Action act = () => storage.Exists("file\0name.txt");

        act.Should()
            .Throw<StoragePathNotAllowedException>()
            .Where(e => e.Reason.Contains("null byte"));
        driver.Verify(
            d => d.FileExists(It.IsAny<string>()),
            Times.Never,
            "driver must not be called for paths with null bytes"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-enforce-{Guid.NewGuid():N}");
        string insidePath = "media/movies/avatar.mkv";
        string outsidePath = "../../../etc/passwd";

        Mock<IStorageDriver> driver = NewDriver();
        // Match on full paths resolved against the root
        driver
            .Setup(d => d.FileExists(It.IsAny<string>()))
            .Returns<string>(p => p.Contains("avatar"));
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        // Inside should succeed
        bool insideExists = storage.Exists(insidePath);
        insideExists.Should().BeTrue("path under root must be accepted");

        // Outside should fail
        Action actOutside = () => storage.Exists(outsidePath);
        actOutside
            .Should()
            .Throw<StoragePathNotAllowedException>("traversal outside root must be rejected");

        driver.Verify(
            d => d.FileExists(It.IsAny<string>()),
            Times.Once,
            "driver must be called for inside path"
        );
    }

    [Fact]
    public void Multiple_allowed_roots_path_accepted_under_any_root()
    {
        // A single path may be under any of several configured roots.
        string rootA = Path.Combine(Path.GetTempPath(), $"nm-root-a-{Guid.NewGuid():N}");
        string rootB = Path.Combine(Path.GetTempPath(), $"nm-root-b-{Guid.NewGuid():N}");

        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);

        StoragePathGuard guard = new([rootA, rootB], driver.Object);
        LocalStorage storage = new(driver.Object, guard);

        // This relative path will be resolved under root A (first root in the list)
        bool exists = storage.Exists("file.txt");

        exists.Should().BeTrue("path under any allowed root must be accepted");
        driver.Verify(d => d.FileExists(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Multiple_allowed_roots_path_outside_all_rejected()
    {
        // A path outside ALL configured roots must be rejected.
        string rootA = Path.Combine(Path.GetTempPath(), $"nm-multi-a-{Guid.NewGuid():N}");
        string rootB = Path.Combine(Path.GetTempPath(), $"nm-multi-b-{Guid.NewGuid():N}");
        string outside = Path.Combine(Path.GetTempPath(), $"nm-outside-{Guid.NewGuid():N}");

        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([rootA, rootB], driver.Object);
        LocalStorage storage = new(driver.Object, guard);

        Action act = () => storage.Exists(outside);

        act.Should()
            .Throw<StoragePathNotAllowedException>("path outside all roots must be rejected");
        driver.Verify(
            d => d.FileExists(It.IsAny<string>()),
            Times.Never,
            "driver must not be called for paths outside all roots"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-link-{Guid.NewGuid():N}");
        string linkPath = Path.Combine(root, "escape-link");
        string realTarget = Path.Combine(Path.GetTempPath(), "outside", "real.txt");

        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(d => d.GetFullPath(It.IsAny<string>()))
            .Returns<string>(p =>
            {
                // Simulate symlink resolution: the link path resolves to the outside target
                if (p.Contains("escape-link"))
                    return realTarget;
                return Path.GetFullPath(p);
            });
        driver
            .Setup(d => d.ResolveLinkTarget(It.Is<string>(p => p.Contains("escape-link"))))
            .Returns(realTarget);
        driver.Setup(d => d.ResolveLinkTarget(It.IsAny<string>())).Returns((string?)null);

        StoragePathGuard guard = new([root], driver.Object);

        Action act = () => guard.Validate("escape-link");

        act.Should()
            .Throw<StoragePathNotAllowedException>("symlinks escaping the root must be rejected");
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-combine-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectorySeparator).Returns('/');
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string combined = storage.CombinePath("movies", "avatar/2009");

        combined
            .Should()
            .NotContain("\\", "CombinePath must not produce OS backslashes on Windows");
        combined
            .Should()
            .Be("movies/avatar/2009", "CombinePath must use forward slashes for storage paths");
    }

    [Fact]
    public void CombinePath_trims_redundant_separators()
    {
        // Inputs like "parent/" + "/child" should produce "parent/child", not "parent//child".
        string root = Path.Combine(Path.GetTempPath(), $"nm-trim-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectorySeparator).Returns('/');
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string combined = storage.CombinePath("parent/", "/child");

        combined.Should().Be("parent/child", "redundant separators must be trimmed");
    }

    [Fact]
    public void CombinePath_handles_empty_parent_or_child()
    {
        // Empty parent: result is child.
        // Empty child: result is parent.
        string root = Path.Combine(Path.GetTempPath(), $"nm-empty-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectorySeparator).Returns('/');
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string emptyParent = storage.CombinePath("", "child");
        string emptyChild = storage.CombinePath("parent", "");

        emptyParent.Should().Be("child", "empty parent should return child");
        emptyChild.Should().Be("parent", "empty child should return parent");
    }

    // ────────────────────────────────────────────────────────────────────
    // GetName, GetParent, GetNameWithoutExtension (scope-relative operations)
    // ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("file.txt", "file.txt")]
    [InlineData("dir/file.txt", "file.txt")]
    [InlineData("a/b/c/d.mkv", "d.mkv")]
    [InlineData("a/b/c/d.mkv/", "d.mkv")] // trailing slash should be ignored
    public void GetName_returns_last_segment_of_path(string path, string expectedName)
    {
        // GetName must work on scope-relative paths without touching the filesystem.
        string root = Path.Combine(Path.GetTempPath(), $"nm-getname-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string name = storage.GetName(path);

        name.Should().Be(expectedName, "GetName must return the last path segment");
    }

    [Theory]
    [InlineData("file.txt", null)]
    [InlineData("dir/file.txt", "dir")]
    [InlineData("a/b/c/d.mkv", "a/b/c")]
    [InlineData("", null)]
    public void GetParent_returns_parent_directory_segment(string path, string? expectedParent)
    {
        // GetParent returns the directory containing the path,
        // or null if the path is already at the scope root.
        string root = Path.Combine(Path.GetTempPath(), $"nm-parent-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string? parent = storage.GetParent(path);

        parent
            .Should()
            .Be(expectedParent, "GetParent must return the directory or null for root entries");
    }

    [Theory]
    [InlineData("file.txt", "file")]
    [InlineData("archive.tar.gz", "archive.tar")]
    [InlineData("dir/noext", "noext")]
    [InlineData("dir/multi.dot.txt", "multi.dot")]
    public void GetNameWithoutExtension_strips_extension_from_name(string path, string expectedName)
    {
        // This is GetName + extension stripping: get the last segment, then strip its extension.
        string root = Path.Combine(Path.GetTempPath(), $"nm-namenoext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string name = storage.GetNameWithoutExtension(path);

        name.Should()
            .Be(
                expectedName,
                "GetNameWithoutExtension must return the last segment without its extension"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-write-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream sink = new();
        driver.Setup(d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(sink);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        await storage.WriteAsync(
            "deep/nested/output/file.mp4",
            [0xAA, 0xBB],
            CancellationToken.None
        );

        driver.Verify(
            d => d.CreateDirectory(It.IsAny<string>()),
            Times.AtLeastOnce(),
            "parent directories must be created before write"
        );
        sink.ToArray().Should().Equal([0xAA, 0xBB], "payload must be written to the stream");
    }

    [Fact]
    public async Task OpenWriteAsync_creates_parent_directories_before_opening_stream()
    {
        // Same guarantee as WriteAsync: parents exist before the stream is returned.
        string root = Path.Combine(Path.GetTempPath(), $"nm-openw-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream stream = new();
        driver.Setup(d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(stream);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Stream result = await storage.OpenWriteAsync(
            "dir/subdir/file.mp4",
            overwrite: true,
            CancellationToken.None
        );

        driver.Verify(
            d => d.CreateDirectory(It.IsAny<string>()),
            Times.AtLeastOnce(),
            "parent directories must exist before OpenWrite returns"
        );
        result.Should().BeSameAs(stream, "the driver's stream must be returned, not wrapped");
    }

    // ────────────────────────────────────────────────────────────────────
    // Move and Copy: both paths validated, parents ensured
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveAsync_validates_source_and_destination_independently()
    {
        // Discriminating fixture: valid source, invalid dest (traversal).
        // Prove the validation catches the bad dest before the move.
        string root = Path.Combine(Path.GetTempPath(), $"nm-move-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Func<Task> act = async () =>
            await storage.MoveAsync("valid/source.txt", "../../../escape", CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                "bad destination path must be rejected even if source is valid"
            );
        driver.Verify(
            d => d.MoveFile(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "driver must not be called for paths with validation errors"
        );
    }

    [Fact]
    public async Task CopyAsync_creates_parent_directory_for_destination()
    {
        // CopyAsync must ensure the destination's parent exists.
        string root = Path.Combine(Path.GetTempPath(), $"nm-copy-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);
        driver
            .Setup(d => d.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback(() => { }); // no-op

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        await storage.CopyAsync("source.txt", "deep/nested/dest.txt", CancellationToken.None);

        driver.Verify(
            d => d.CreateDirectory(It.IsAny<string>()),
            Times.AtLeastOnce(),
            "destination parent directories must be created"
        );
        driver.Verify(
            d => d.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Once(),
            "copy must be called after parents are ensured"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-list-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        // Driver returns OS-absolute paths (its contract)
        string absoluteFile = Path.Combine(root, "subdir", "file.txt");
        string absoluteDir = Path.Combine(root, "subdir", "childdir");

        driver
            .Setup(d =>
                d.EnumerateEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns([
                new StorageEntryInfo(absoluteFile, false, 42L, DateTime.UtcNow),
                new StorageEntryInfo(absoluteDir, true, 0L, DateTime.UtcNow),
            ]);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        IReadOnlyList<StorageEntry> result = storage.List("", null, recursive: false);

        result.Should().HaveCount(2, "all entries must be returned");
        result[0].Path.Should().NotStartWith("/", "scope-relative paths must not start with /");
        result[0].Path.Should().NotStartWith("\\", "scope-relative paths must not start with \\");
        result[0]
            .Path.Should()
            .NotStartWith(root, "scope-relative paths must not include the root prefix");
    }

    [Fact]
    public void List_applies_pattern_filter_via_driver()
    {
        // The pattern parameter is passed to the driver's enumeration.
        // Only matching files should be returned.
        string root = Path.Combine(Path.GetTempPath(), $"nm-pattern-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(d =>
                d.EnumerateEntries(It.IsAny<string>(), "*.mkv", SearchOption.TopDirectoryOnly)
            )
            .Returns([
                new StorageEntryInfo(
                    Path.Combine(root, "movie.mkv"),
                    false,
                    1024L,
                    DateTime.UtcNow
                ),
            ]);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        IReadOnlyList<StorageEntry> result = storage.List("", "*.mkv", recursive: false);

        result.Should().ContainSingle("pattern filter must be applied");
        result[0].Path.Should().EndWith(".mkv", "only matching files must be returned");
        driver.Verify(
            d => d.EnumerateEntries(It.IsAny<string>(), "*.mkv", SearchOption.TopDirectoryOnly),
            Times.Once(),
            "pattern must be passed to the driver"
        );
    }

    [Fact]
    public void List_recursive_true_passes_AllDirectories_to_driver()
    {
        // Recursive=true should traverse the entire subtree.
        string root = Path.Combine(Path.GetTempPath(), $"nm-recurse-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(d =>
                d.EnumerateEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    SearchOption.AllDirectories
                )
            )
            .Returns([
                new StorageEntryInfo(Path.Combine(root, "a.txt"), false, 100L, DateTime.UtcNow),
                new StorageEntryInfo(
                    Path.Combine(root, "deep", "b.txt"),
                    false,
                    100L,
                    DateTime.UtcNow
                ),
                new StorageEntryInfo(
                    Path.Combine(root, "deep", "deeper", "c.txt"),
                    false,
                    100L,
                    DateTime.UtcNow
                ),
            ]);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        IReadOnlyList<StorageEntry> result = storage.List("", null, recursive: true);

        result.Should().HaveCount(3, "all files in the tree must be returned");
        driver.Verify(
            d =>
                d.EnumerateEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    SearchOption.AllDirectories
                ),
            Times.Once(),
            "recursive=true must pass AllDirectories to the driver"
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
        string root = Path.Combine(Path.GetTempPath(), $"nm-exists-file-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver
            .Setup(d => d.FileExists(It.IsAny<string>()))
            .Returns<string>(p => p.EndsWith("file.txt"));
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        bool exists = storage.Exists("file.txt");

        exists.Should().BeTrue("existing files must return true");
        driver.Verify(d => d.FileExists(It.IsAny<string>()), Times.Once());
    }

    [Fact]
    public void Exists_returns_true_for_directory()
    {
        // Exists must also return true for directories.
        string root = Path.Combine(Path.GetTempPath(), $"nm-exists-dir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(false);
        driver
            .Setup(d => d.DirectoryExists(It.IsAny<string>()))
            .Returns<string>(p => p.EndsWith("somedir"));

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        bool exists = storage.Exists("somedir");

        exists.Should().BeTrue("existing directories must return true");
        driver.Verify(d => d.DirectoryExists(It.IsAny<string>()), Times.Once());
    }

    [Fact]
    public void Exists_returns_false_when_neither_file_nor_directory()
    {
        // Exists returns false only when both checks fail.
        string root = Path.Combine(Path.GetTempPath(), $"nm-notexists-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(false);
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        bool exists = storage.Exists("nonexistent.txt");

        exists.Should().BeFalse("nonexistent paths must return false");
    }

    // ────────────────────────────────────────────────────────────────────
    // SizeOrZero: zero for missing, size for existing
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void SizeOrZero_returns_size_when_file_exists()
    {
        // SizeOrZero avoids the (await Exists) ? (await Size) : 0 pattern.
        string root = Path.Combine(Path.GetTempPath(), $"nm-sizeorz-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);
        driver.Setup(d => d.GetFileSize(It.IsAny<string>())).Returns(12345L);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        long size = storage.SizeOrZero("file.mp4");

        size.Should().Be(12345L, "size must be returned for existing files");
    }

    [Fact]
    public void SizeOrZero_returns_zero_when_file_missing()
    {
        // Missing files return 0, not an exception.
        string root = Path.Combine(Path.GetTempPath(), $"nm-size0-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(false);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        long size = storage.SizeOrZero("missing.mp4");

        size.Should().Be(0L, "missing files must return 0, not throw");
    }

    // ────────────────────────────────────────────────────────────────────
    // Delete operations: file vs. directory
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_is_no_op_when_file_missing()
    {
        // Delete must be idempotent: deleting a nonexistent file is safe.
        string root = Path.Combine(Path.GetTempPath(), $"nm-del-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(false);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        storage.Delete("nonexistent.txt");

        driver.Verify(
            d => d.DeleteFile(It.IsAny<string>()),
            Times.Never,
            "delete must not call driver for missing files"
        );
    }

    [Fact]
    public void Delete_calls_driver_when_file_present()
    {
        // Delete must delegate to the driver when the file exists.
        string root = Path.Combine(Path.GetTempPath(), $"nm-delfile-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        storage.Delete("exists.txt");

        driver.Verify(d => d.DeleteFile(It.IsAny<string>()), Times.Once());
    }

    [Fact]
    public void DeleteDirectory_with_recursive_true_clears_subtree()
    {
        // DeleteDirectory(path, recursive: true) must remove the directory
        // and all its contents.
        string root = Path.Combine(Path.GetTempPath(), $"nm-deldir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(true);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        storage.DeleteDirectory("old-library", recursive: true);

        driver.Verify(
            d => d.DeleteDirectory(It.IsAny<string>(), true),
            Times.Once(),
            "recursive deletion must pass true to the driver"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Create directory: idempotent
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void CreateDirectory_delegates_to_driver()
    {
        // CreateDirectory must call the driver's CreateDirectory method.
        string root = Path.Combine(Path.GetTempPath(), $"nm-mkdir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        storage.CreateDirectory("new/nested/dir");

        driver.Verify(d => d.CreateDirectory(It.IsAny<string>()), Times.Once());
    }

    // ────────────────────────────────────────────────────────────────────
    // DirectorySeparator and Driver property
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public void DirectorySeparator_returns_driver_separator()
    {
        // LocalStorage's separator is the driver's; remote drivers use '/'.
        string root = Path.Combine(Path.GetTempPath(), $"nm-sep-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectorySeparator).Returns('/');

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        char sep = storage.DirectorySeparator;

        sep.Should().Be('/', "separator must come from the driver");
    }

    [Fact]
    public void Driver_property_exposes_underlying_driver()
    {
        // IStorage.Driver allows consumers to access the low-level backend
        // when they need it (e.g., MediaScan).
        string root = Path.Combine(Path.GetTempPath(), $"nm-driver-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        IStorageDriver result = storage.Driver;

        result.Should().BeSameAs(driver.Object, "Driver property must expose the driver");
    }

    // ────────────────────────────────────────────────────────────────────
    // Hash: algorithm validation, lowercase hex output
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HashAsync_supports_sha256()
    {
        // Hash must support "sha256" (case-insensitive).
        string root = Path.Combine(Path.GetTempPath(), $"nm-hash-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        byte[] fileData = [0x48, 0x65, 0x6C, 0x6C, 0x6F]; // "Hello"
        driver.Setup(d => d.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(fileData));

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string hash = await storage.HashAsync("file.txt", "sha256", CancellationToken.None);

        hash.Should().NotBeNullOrEmpty("hash must be computed");
        hash.Should().Be(hash.ToLowerInvariant(), "hash must be lowercase hex");
        hash.Length.Should().Be(64, "SHA256 produces 32 bytes = 64 hex chars");
    }

    [Fact]
    public async Task HashAsync_supports_md5()
    {
        // Hash must also support "md5".
        string root = Path.Combine(Path.GetTempPath(), $"nm-md5-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.OpenRead(It.IsAny<string>())).Returns(new MemoryStream([0x61])); // "a"

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string hash = await storage.HashAsync("file.txt", "md5", CancellationToken.None);

        hash.Should().NotBeNullOrEmpty("md5 hash must be computed");
        hash.Length.Should().Be(32, "MD5 produces 16 bytes = 32 hex chars");
    }

    [Fact]
    public async Task HashAsync_rejects_unsupported_algorithm()
    {
        // Only sha256 and md5 are supported.
        string root = Path.Combine(Path.GetTempPath(), $"nm-nosuch-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Func<Task> act = async () =>
            await storage.HashAsync("file.txt", "blake2b", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>("unsupported algorithms must be rejected");
    }

    // ────────────────────────────────────────────────────────────────────
    // AcquireLocalPath: lease for child process consumption
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireLocalPathAsync_returns_absolute_path()
    {
        // AcquireLocalPath returns an OS-absolute path for child process use.
        // This path is NOT scope-relative (it escapes the abstraction by design).
        string root = Path.Combine(Path.GetTempPath(), $"nm-lease-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        LocalPathLease lease = await storage.AcquireLocalPathAsync(
            "media/video.mp4",
            CancellationToken.None
        );

        lease.Path.Should().NotBeNullOrEmpty("lease must contain a path");
        Path.IsPathRooted(lease.Path)
            .Should()
            .BeTrue("lease path must be OS-absolute for child process use");
        lease.Dispose();
    }

    // ────────────────────────────────────────────────────────────────────
    // ReadAllTextAsync / WriteAllTextAsync: text content operations
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAllTextAsync_writes_string_content()
    {
        // WriteAllTextAsync is a convenience for text content.
        string root = Path.Combine(Path.GetTempPath(), $"nm-writetext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        driver.Setup(d => d.DirectoryExists(It.IsAny<string>())).Returns(false);
        MemoryStream sink = new();
        driver.Setup(d => d.OpenWrite(It.IsAny<string>(), It.IsAny<bool>())).Returns(sink);

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        await storage.WriteAllTextAsync(
            "config.json",
            "{\"key\": \"value\"}",
            CancellationToken.None
        );

        sink.ToArray().Should().Equal(System.Text.Encoding.UTF8.GetBytes("{\"key\": \"value\"}"));
    }

    [Fact]
    public async Task ReadAllTextAsync_reads_text_content()
    {
        // ReadAllTextAsync decodes bytes to a string.
        string root = Path.Combine(Path.GetTempPath(), $"nm-readtext-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        string content = "line1\nline2\nline3";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        driver.Setup(d => d.OpenRead(It.IsAny<string>())).Returns(new MemoryStream(bytes));

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        string result = await storage.ReadAllTextAsync("file.txt", CancellationToken.None);

        result.Should().Be(content, "text content must be read correctly");
    }

    // ────────────────────────────────────────────────────────────────────
    // MoveDirectory / MoveDirectoryAsync
    // ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveDirectoryAsync_validates_both_paths()
    {
        // MoveDirectory must validate both source and destination paths.
        string root = Path.Combine(Path.GetTempPath(), $"nm-movedir-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();

        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        // Moving to a path with traversal should fail validation
        Func<Task> act = async () =>
            await storage.MoveDirectoryAsync("old", "../../../escape", CancellationToken.None);

        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>("bad destination path must be rejected");
        driver.Verify(
            d => d.MoveDirectory(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "driver must not be called for invalid paths"
        );
    }

    // ────────────────────────────────────────────────────────────────────
    // Rejection of Windows device paths
    // ────────────────────────────────────────────────────────────────────

    [SkippableFact]
    public void Reject_device_paths_on_windows()
    {
        Skip.IfNot(OperatingSystem.IsWindows(), "Device paths are Windows-specific");

        string root = Path.Combine(Path.GetTempPath(), $"nm-devpath-{Guid.NewGuid():N}");
        Mock<IStorageDriver> driver = NewDriver();
        StoragePathGuard guard = new([root], driver.Object);
        IStorage storage = new LocalStorage(driver.Object, guard);

        Action act = () => storage.Exists(@"\\?\C:\Windows");

        act.Should()
            .Throw<StoragePathNotAllowedException>("device paths must be rejected on Windows");
    }
}
