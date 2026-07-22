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
/// Full end-to-end exercise of <see cref="LocalStorage"/> against a
/// real temp directory using the real
/// <see cref="LocalStorageDriver"/>.
/// </summary>
public class LocalStorageIntegrationTests : IDisposable
{
    private readonly string _root;
    private readonly LocalStorage _storage;

    public LocalStorageIntegrationTests()
    {
        _root = Path.Combine(path1: Path.GetTempPath(), path2: "nm-storage-it-" + Path.GetRandomFileName());
        Directory.CreateDirectory(path: _root);
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [_root], driver: driver);
        _storage = new(driver: driver, guard: guard);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path: _root))
                Directory.Delete(path: _root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task Write_then_read_round_trips_bytes()
    {
        string path = Path.Combine(path1: _root, path2: "round-trip.bin");
        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF];

        await _storage.WriteAsync(path: path, bytes: payload, ct: CancellationToken.None);
        byte[] back = await _storage.ReadAsync(path: path, ct: CancellationToken.None);

        back.Should().Equal(elements: payload);
    }

    [Fact]
    public async Task OpenWriteAsync_overwrite_false_throws_when_file_exists()
    {
        string path = Path.Combine(path1: _root, path2: "exists.bin");
        await _storage.WriteAsync(path: path, bytes: [0x00], ct: CancellationToken.None);

        Func<Task> act = async () =>
        {
            await using Stream s = await _storage.OpenWriteAsync(
                path: path,
                overwrite: false,
                ct: CancellationToken.None
            );
        };

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task ExistsAsync_reports_files_and_directories()
    {
        string filePath = Path.Combine(path1: _root, path2: "exists-test.bin");
        string dirPath = Path.Combine(path1: _root, path2: "exists-dir");
        await _storage.WriteAsync(path: filePath, bytes: [0x01], ct: CancellationToken.None);
        await _storage.CreateDirectoryAsync(path: dirPath, ct: CancellationToken.None);

        (await _storage.ExistsAsync(path: filePath, ct: CancellationToken.None)).Should().BeTrue();
        (await _storage.ExistsAsync(path: dirPath, ct: CancellationToken.None)).Should().BeTrue();
        (await _storage.ExistsAsync(path: Path.Combine(path1: _root, path2: "missing.bin"), ct: CancellationToken.None))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void GetFullPath_resolves_scope_relative_key_under_library_root_not_process_cwd()
    {
        // Guards the local-library scan/move regression: a scope-relative folder
        // key ("Anime/Anime/Show") must resolve under the configured library root.
        // Resolving it through the raw driver instead of the facade produced the
        // bug — LocalStorageDriver.GetFullPath is a bare Path.GetFullPath that
        // canonicalizes against the process CWD (/app in the container), so the key
        // resolved outside the library root and every local rescan found zero files.
        Directory.CreateDirectory(path: Path.Combine(path1: _root, path2: "Anime", path3: "Anime"));
        IStorage storage = _storage;

        string viaFacade = storage.GetFullPath(path: "Anime/Anime");
        viaFacade.Should().StartWith(expected: _root);
        Directory.Exists(path: viaFacade).Should().BeTrue();
        storage.Exists(path: "Anime/Anime").Should().BeTrue();

        // The trap the scan fell into: the raw driver is root-blind and resolves
        // relative to the process CWD, not the library root.
        string viaDriver = storage.Driver.GetFullPath(path: "Anime/Anime");
        viaDriver.Should().NotBe(unexpected: viaFacade);
        Directory.Exists(path: viaDriver).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_removes_file_then_no_op_on_missing()
    {
        string path = Path.Combine(path1: _root, path2: "to-delete.bin");
        await _storage.WriteAsync(path: path, bytes: [0x01], ct: CancellationToken.None);
        await _storage.DeleteAsync(path: path, ct: CancellationToken.None);

        File.Exists(path: path).Should().BeFalse();
        // Second delete must not throw.
        await _storage.DeleteAsync(path: path, ct: CancellationToken.None);
    }

    [Fact]
    public async Task DeleteDirectoryAsync_recursive_clears_subtree()
    {
        string dir = Path.Combine(path1: _root, path2: "subtree");
        await _storage.WriteAsync(path: Path.Combine(path1: dir, path2: "a.txt"), bytes: [0xAA], ct: CancellationToken.None);
        await _storage.WriteAsync(
            path: Path.Combine(path1: dir, path2: "nested", path3: "b.txt"),
            bytes: [0xBB],
            ct: CancellationToken.None
        );

        await _storage.DeleteDirectoryAsync(path: dir, recursive: true, ct: CancellationToken.None);

        Directory.Exists(path: dir).Should().BeFalse();
    }

    [Fact]
    public async Task MoveAsync_relocates_file_and_creates_destination_parent()
    {
        string from = Path.Combine(path1: _root, path2: "src.bin");
        string to = Path.Combine(path1: _root, path2: "dest", path3: "moved.bin");
        await _storage.WriteAsync(path: from, bytes: [0xC0, 0xFE], ct: CancellationToken.None);

        await _storage.MoveAsync(from: from, to: to, ct: CancellationToken.None);

        File.Exists(path: from).Should().BeFalse();
        File.Exists(path: to).Should().BeTrue();
    }

    [Fact]
    public async Task CopyAsync_overwrites_existing_target()
    {
        string from = Path.Combine(path1: _root, path2: "copy-src.bin");
        string to = Path.Combine(path1: _root, path2: "copy-dst.bin");
        await _storage.WriteAsync(path: from, bytes: [0x11, 0x22], ct: CancellationToken.None);
        await _storage.WriteAsync(path: to, bytes: [0x99], ct: CancellationToken.None);

        await _storage.CopyAsync(from: from, to: to, ct: CancellationToken.None);

        File.Exists(path: from).Should().BeTrue();
        (await _storage.ReadAsync(path: to, ct: CancellationToken.None)).Should().Equal(elements: [0x11, 0x22]);
    }

    [Fact]
    public async Task SizeAsync_returns_actual_file_size()
    {
        string path = Path.Combine(path1: _root, path2: "sized.bin");
        byte[] payload = new byte[2048];
        await _storage.WriteAsync(path: path, bytes: payload, ct: CancellationToken.None);

        long size = await _storage.SizeAsync(path: path, ct: CancellationToken.None);

        size.Should().Be(expected: 2048);
    }

    [Fact]
    public async Task LastModifiedAsync_returns_recent_utc_timestamp()
    {
        string path = Path.Combine(path1: _root, path2: "stamped.bin");
        await _storage.WriteAsync(path: path, bytes: [0x01], ct: CancellationToken.None);

        DateTimeOffset stamp = await _storage.LastModifiedAsync(path: path, ct: CancellationToken.None);

        stamp.Offset.Should().Be(expected: TimeSpan.Zero);
        (DateTimeOffset.UtcNow - stamp).Should().BeLessThan(expected: TimeSpan.FromMinutes(minutes: 1));
    }

    [Fact]
    public async Task ListAsync_recursive_with_pattern_filters_correctly()
    {
        await _storage.WriteAsync(path: Path.Combine(path1: _root, path2: "a.txt"), bytes: [0x01], ct: CancellationToken.None);
        await _storage.WriteAsync(path: Path.Combine(path1: _root, path2: "b.bin"), bytes: [0x02], ct: CancellationToken.None);
        await _storage.WriteAsync(
            path: Path.Combine(path1: _root, path2: "sub", path3: "c.txt"),
            bytes: [0x03],
            ct: CancellationToken.None
        );

        List<StorageEntry> recursive = [];
        await foreach (
            StorageEntry e in _storage.ListAsync(
                path: _root,
                pattern: "*.txt",
                recursive: true,
                ct: CancellationToken.None
            )
        )
            recursive.Add(item: e);

        recursive.Select(selector: e => Path.GetFileName(path: e.Path)).Should().BeEquivalentTo(expectation: ["a.txt", "c.txt"]);
        recursive.All(predicate: e => !e.IsDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task HashAsync_sha256_matches_real_file_content()
    {
        // SHA-256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        string path = Path.Combine(path1: _root, path2: "hello.txt");
        await _storage.WriteAsync(path: path, bytes: "hello"u8.ToArray(), ct: CancellationToken.None);

        string digest = await _storage.HashAsync(path: path, algorithm: "sha256", ct: CancellationToken.None);

        digest.Should().Be(expected: "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_real_path()
    {
        string path = Path.Combine(path1: _root, path2: "lease.bin");
        await _storage.WriteAsync(path: path, bytes: [0x42], ct: CancellationToken.None);

        await using LocalPathLease lease = await _storage.AcquireLocalPathAsync(
            path: path,
            ct: CancellationToken.None
        );

        lease.Path.Should().Be(expected: Path.GetFullPath(path: path));
        File.Exists(path: lease.Path).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_rejects_path_outside_allowed_root()
    {
        string outside = Path.Combine(path1: Path.GetTempPath(), path2: "definitely-not-root.bin");

        Func<Task> act = () => _storage.WriteAsync(path: outside, bytes: [0x00], ct: CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task Symlink_escape_is_rejected()
    {
        if (!RuntimeSupportsSymlinks())
            return;

        string outsideDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nm-storage-outside-" + Path.GetRandomFileName()
        );
        Directory.CreateDirectory(path: outsideDir);
        try
        {
            string realTarget = Path.Combine(path1: outsideDir, path2: "secret.bin");
            await File.WriteAllBytesAsync(path: realTarget, bytes: [0xFF], cancellationToken: CancellationToken.None);

            string linkPath = Path.Combine(path1: _root, path2: "escape-link");
            try
            {
                File.CreateSymbolicLink(path: linkPath, pathToTarget: realTarget);
            }
            catch (UnauthorizedAccessException)
            {
                // Windows non-developer-mode users can't create symlinks; skip.
                return;
            }

            Func<Task> act = () => _storage.ReadAsync(path: linkPath, ct: CancellationToken.None);

            await act.Should().ThrowAsync<StoragePathNotAllowedException>();
        }
        finally
        {
            try
            {
                Directory.Delete(path: outsideDir, recursive: true);
            }
            catch
            { /* best-effort */
            }
        }
    }

    private static bool RuntimeSupportsSymlinks()
    {
        // POSIX always supports symlinks. Windows requires either admin or
        // Developer Mode — we still try and let UnauthorizedAccessException
        // skip the test if it fails.
        return true;
    }
}
