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
        _root = Path.Combine(Path.GetTempPath(), "nm-storage-it-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
        LocalStorageDriver driver = new();
        StoragePathGuard guard = new([_root], driver);
        _storage = new(driver, guard);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public async Task Write_then_read_round_trips_bytes()
    {
        string path = Path.Combine(_root, "round-trip.bin");
        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF];

        await _storage.WriteAsync(path, payload, CancellationToken.None);
        byte[] back = await _storage.ReadAsync(path, CancellationToken.None);

        back.Should().Equal(payload);
    }

    [Fact]
    public async Task OpenWriteAsync_overwrite_false_throws_when_file_exists()
    {
        string path = Path.Combine(_root, "exists.bin");
        await _storage.WriteAsync(path, [0x00], CancellationToken.None);

        Func<Task> act = async () =>
        {
            await using Stream s = await _storage.OpenWriteAsync(
                path,
                false,
                CancellationToken.None
            );
        };

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task ExistsAsync_reports_files_and_directories()
    {
        string filePath = Path.Combine(_root, "exists-test.bin");
        string dirPath = Path.Combine(_root, "exists-dir");
        await _storage.WriteAsync(filePath, [0x01], CancellationToken.None);
        await _storage.CreateDirectoryAsync(dirPath, CancellationToken.None);

        (await _storage.ExistsAsync(filePath, CancellationToken.None)).Should().BeTrue();
        (await _storage.ExistsAsync(dirPath, CancellationToken.None)).Should().BeTrue();
        (await _storage.ExistsAsync(Path.Combine(_root, "missing.bin"), CancellationToken.None))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_removes_file_then_no_op_on_missing()
    {
        string path = Path.Combine(_root, "to-delete.bin");
        await _storage.WriteAsync(path, [0x01], CancellationToken.None);
        await _storage.DeleteAsync(path, CancellationToken.None);

        File.Exists(path).Should().BeFalse();
        // Second delete must not throw.
        await _storage.DeleteAsync(path, CancellationToken.None);
    }

    [Fact]
    public async Task DeleteDirectoryAsync_recursive_clears_subtree()
    {
        string dir = Path.Combine(_root, "subtree");
        await _storage.WriteAsync(Path.Combine(dir, "a.txt"), [0xAA], CancellationToken.None);
        await _storage.WriteAsync(
            Path.Combine(dir, "nested", "b.txt"),
            [0xBB],
            CancellationToken.None
        );

        await _storage.DeleteDirectoryAsync(dir, recursive: true, CancellationToken.None);

        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public async Task MoveAsync_relocates_file_and_creates_destination_parent()
    {
        string from = Path.Combine(_root, "src.bin");
        string to = Path.Combine(_root, "dest", "moved.bin");
        await _storage.WriteAsync(from, [0xC0, 0xFE], CancellationToken.None);

        await _storage.MoveAsync(from, to, CancellationToken.None);

        File.Exists(from).Should().BeFalse();
        File.Exists(to).Should().BeTrue();
    }

    [Fact]
    public async Task CopyAsync_overwrites_existing_target()
    {
        string from = Path.Combine(_root, "copy-src.bin");
        string to = Path.Combine(_root, "copy-dst.bin");
        await _storage.WriteAsync(from, [0x11, 0x22], CancellationToken.None);
        await _storage.WriteAsync(to, [0x99], CancellationToken.None);

        await _storage.CopyAsync(from, to, CancellationToken.None);

        File.Exists(from).Should().BeTrue();
        (await _storage.ReadAsync(to, CancellationToken.None)).Should().Equal(0x11, 0x22);
    }

    [Fact]
    public async Task SizeAsync_returns_actual_file_size()
    {
        string path = Path.Combine(_root, "sized.bin");
        byte[] payload = new byte[2048];
        await _storage.WriteAsync(path, payload, CancellationToken.None);

        long size = await _storage.SizeAsync(path, CancellationToken.None);

        size.Should().Be(2048);
    }

    [Fact]
    public async Task LastModifiedAsync_returns_recent_utc_timestamp()
    {
        string path = Path.Combine(_root, "stamped.bin");
        await _storage.WriteAsync(path, [0x01], CancellationToken.None);

        DateTimeOffset stamp = await _storage.LastModifiedAsync(path, CancellationToken.None);

        stamp.Offset.Should().Be(TimeSpan.Zero);
        (DateTimeOffset.UtcNow - stamp).Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ListAsync_recursive_with_pattern_filters_correctly()
    {
        await _storage.WriteAsync(Path.Combine(_root, "a.txt"), [0x01], CancellationToken.None);
        await _storage.WriteAsync(Path.Combine(_root, "b.bin"), [0x02], CancellationToken.None);
        await _storage.WriteAsync(
            Path.Combine(_root, "sub", "c.txt"),
            [0x03],
            CancellationToken.None
        );

        List<StorageEntry> recursive = [];
        await foreach (
            StorageEntry e in _storage.ListAsync(
                _root,
                "*.txt",
                recursive: true,
                CancellationToken.None
            )
        )
            recursive.Add(e);

        recursive.Select(e => Path.GetFileName(e.Path)).Should().BeEquivalentTo("a.txt", "c.txt");
        recursive.All(e => !e.IsDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task HashAsync_sha256_matches_real_file_content()
    {
        // SHA-256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        string path = Path.Combine(_root, "hello.txt");
        await _storage.WriteAsync(path, "hello"u8.ToArray(), CancellationToken.None);

        string digest = await _storage.HashAsync(path, "sha256", CancellationToken.None);

        digest.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public async Task AcquireLocalPathAsync_returns_real_path()
    {
        string path = Path.Combine(_root, "lease.bin");
        await _storage.WriteAsync(path, [0x42], CancellationToken.None);

        await using LocalPathLease lease = await _storage.AcquireLocalPathAsync(
            path,
            CancellationToken.None
        );

        lease.Path.Should().Be(Path.GetFullPath(path));
        File.Exists(lease.Path).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_rejects_path_outside_allowed_root()
    {
        string outside = Path.Combine(Path.GetTempPath(), "definitely-not-root.bin");

        Func<Task> act = () => _storage.WriteAsync(outside, [0x00], CancellationToken.None);

        await act.Should().ThrowAsync<StoragePathNotAllowedException>();
    }

    [Fact]
    public async Task Symlink_escape_is_rejected()
    {
        if (!RuntimeSupportsSymlinks())
            return;

        string outsideDir = Path.Combine(
            Path.GetTempPath(),
            "nm-storage-outside-" + Path.GetRandomFileName()
        );
        Directory.CreateDirectory(outsideDir);
        try
        {
            string realTarget = Path.Combine(outsideDir, "secret.bin");
            await File.WriteAllBytesAsync(realTarget, [0xFF], CancellationToken.None);

            string linkPath = Path.Combine(_root, "escape-link");
            try
            {
                File.CreateSymbolicLink(linkPath, realTarget);
            }
            catch (UnauthorizedAccessException)
            {
                // Windows non-developer-mode users can't create symlinks; skip.
                return;
            }

            Func<Task> act = () => _storage.ReadAsync(linkPath, CancellationToken.None);

            await act.Should().ThrowAsync<StoragePathNotAllowedException>();
        }
        finally
        {
            try
            {
                Directory.Delete(outsideDir, recursive: true);
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
