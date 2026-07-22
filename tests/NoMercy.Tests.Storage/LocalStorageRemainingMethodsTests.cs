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
/// Fills the gaps <see cref="IStorageFacadeTests"/> leaves in
/// <see cref="LocalStorage"/>: constructor null-guards, the sync/async
/// method pairs it doesn't exercise (OpenReadAsync, Size, LastModified,
/// OpenRead, OpenWrite, MoveDirectory/Async), and the
/// "listed entry equals the scope root exactly" branch of
/// <c>ToScopeRelative</c>. Uses a REAL <see cref="LocalStorageDriver"/>
/// against a REAL temp directory — this facade's whole job is talking to
/// the OS filesystem, so a mock of that filesystem would not prove it works.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class LocalStorageRemainingMethodsTests : IDisposable
{
    private readonly string _root;
    private readonly LocalStorageDriver _driver = new();
    private readonly StoragePathGuard _guard;
    private readonly LocalStorage _storage;

    public LocalStorageRemainingMethodsTests()
    {
        _root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-lsrm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _root);
        _guard = new(allowedRoots: [_root], driver: _driver);
        _storage = new(driver: _driver, guard: _guard);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(path: _root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public void Constructor_rejects_null_driver()
    {
        Action act = () => new LocalStorage(driver: null!, guard: _guard);

        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "driver");
    }

    [Fact]
    public void Constructor_rejects_null_guard()
    {
        Action act = () => new LocalStorage(driver: _driver, guard: null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName(paramName: "guard");
    }

    [Fact]
    public async Task OpenReadAsync_returns_a_stream_over_the_real_file()
    {
        File.WriteAllBytes(path: Path.Combine(path1: _root, path2: "clip.bin"), bytes: [1, 2, 3]);

        await using Stream stream = await _storage.OpenReadAsync(
            path: "clip.bin",
            ct: CancellationToken.None
        );
        byte[] buffer = new byte[3];
        await stream.ReadExactlyAsync(buffer: buffer);

        buffer.Should().Equal(elements: [1, 2, 3]);
    }

    [Fact]
    public void Size_returns_real_file_length()
    {
        File.WriteAllBytes(path: Path.Combine(path1: _root, path2: "sized.bin"), bytes: new byte[777]);

        _storage.Size(path: "sized.bin").Should().Be(expected: 777);
    }

    [Fact]
    public void LastModified_reflects_a_recent_real_write()
    {
        File.WriteAllText(path: Path.Combine(path1: _root, path2: "recent.txt"), contents: "x");

        _storage
            .LastModified(path: "recent.txt")
            .UtcDateTime.Should()
            .BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 1));
    }

    [Fact]
    public void OpenRead_sync_returns_a_readable_stream_over_the_real_file()
    {
        File.WriteAllBytes(path: Path.Combine(path1: _root, path2: "readme.bin"), bytes: [9, 8, 7]);

        using Stream stream = _storage.OpenRead(path: "readme.bin");
        byte[] buffer = new byte[3];
        stream.ReadExactly(buffer: buffer);

        buffer.Should().Equal(elements: [9, 8, 7]);
    }

    [Fact]
    public void OpenWrite_sync_creates_parent_directories_and_writes_real_bytes()
    {
        using (Stream stream = _storage.OpenWrite(path: "deep/nested/out.bin", overwrite: true))
            stream.Write(buffer: [4, 5, 6], offset: 0, count: 3);

        File.ReadAllBytes(path: Path.Combine(path1: _root, path2: "deep", path3: "nested", path4: "out.bin")).Should().Equal(elements: [4, 5, 6]);
    }

    [Fact]
    public async Task MoveDirectoryAsync_moves_a_real_directory_tree()
    {
        Directory.CreateDirectory(path: Path.Combine(path1: _root, path2: "olddir"));
        File.WriteAllText(path: Path.Combine(path1: _root, path2: "olddir", path3: "f.txt"), contents: "x");

        await _storage.MoveDirectoryAsync(from: "olddir", to: "newdir", ct: CancellationToken.None);

        Directory.Exists(path: Path.Combine(path1: _root, path2: "olddir")).Should().BeFalse();
        File.Exists(path: Path.Combine(path1: _root, path2: "newdir", path3: "f.txt")).Should().BeTrue();
    }

    [Fact]
    public void MoveDirectory_sync_moves_a_real_directory_tree()
    {
        Directory.CreateDirectory(path: Path.Combine(path1: _root, path2: "olddir2"));
        File.WriteAllText(path: Path.Combine(path1: _root, path2: "olddir2", path3: "f.txt"), contents: "x");

        _storage.MoveDirectory(from: "olddir2", to: "newdir2");

        Directory.Exists(path: Path.Combine(path1: _root, path2: "olddir2")).Should().BeFalse();
        File.Exists(path: Path.Combine(path1: _root, path2: "newdir2", path3: "f.txt")).Should().BeTrue();
    }

    [Fact]
    public void List_entry_whose_absolute_path_equals_the_scope_root_exactly_maps_to_empty_string()
    {
        // Regression for ToScopeRelative's "listed entry equals root exactly"
        // branch: a driver that yields the scope root itself as one of its
        // own entries (some backends self-list the directory node) must map
        // to the empty string — the canonical "this is the root" scope-
        // relative path — not a leaked absolute path or a stray "/".
        Mock<IStorageDriver> driver = new(behavior: MockBehavior.Loose);
        driver.Setup(expression: d => d.GetFullPath(It.IsAny<string>())).Returns<string>(valueFunction: Path.GetFullPath);
        driver.Setup(expression: d => d.ResolveLinkTarget(It.IsAny<string>())).Returns(value: (string?)null);
        driver
            .Setup(expression: d =>
                d.EnumerateEntries(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())
            )
            .Returns(value: [new StorageEntryInfo(Path: _root, IsDirectory: true, Size: 0L, LastWriteUtc: DateTime.UtcNow)]);
        StoragePathGuard guard = new(allowedRoots: [_root], driver: driver.Object);
        LocalStorage storage = new(driver: driver.Object, guard: guard);

        IReadOnlyList<StorageEntry> entries = storage.List(path: "", pattern: null, recursive: false);

        entries.Should().ContainSingle();
        entries[index: 0]
            .Path.Should()
            .Be(expected: string.Empty, because: "the root entry itself must map to the empty scope-relative path");
    }
}
