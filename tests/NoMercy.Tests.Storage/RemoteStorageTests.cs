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

using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;
using NoMercy.Tests.Storage.Fakes;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Behavioral contract tests for <see cref="RemoteStorage"/> — the
/// <see cref="IStorage"/> facade every remote driver (S3/R2, NFS, SMB, WebDAV)
/// is wrapped in. Unlike <see cref="RemoteStoragePathGuardTests"/> (which only
/// proves absolute paths are rejected), this file demands the actual behavior
/// of every facade method: what gets returned, what gets written, and that
/// sync/async companions agree. A real in-memory driver
/// (<see cref="InMemoryStorageDriver"/>) stands in for the network backend so
/// these tests exercise real stream copies and real byte content rather than
/// asserting against a mock's call log.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class RemoteStorageTests
{
    private static RemoteStorage NewStorage(out InMemoryStorageDriver driver)
    {
        driver = new();
        return new(driver: driver);
    }

    // ------------------------------------------------------------------
    // Constructor guard
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_rejects_null_driver()
    {
        Action act = () => new RemoteStorage(driver: null!);

        act.Should()
            .Throw<ArgumentNullException>(because: "RemoteStorage must not silently accept a null driver");
    }

    [Fact]
    public void Driver_property_exposes_underlying_driver()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        storage
            .Driver.Should()
            .BeSameAs(expected: driver, because: "Driver must expose the exact instance passed to the constructor");
    }

    // ------------------------------------------------------------------
    // Read / Write round trip (async)
    // ------------------------------------------------------------------

    [Fact]
    public async Task WriteAsync_then_ReadAsync_round_trips_bytes()
    {
        RemoteStorage storage = NewStorage(driver: out _);
        byte[] payload = [0x01, 0x02, 0x03, 0xFF];

        await storage.WriteAsync(path: "movies/avatar.mkv", bytes: payload, ct: CancellationToken.None);
        byte[] result = await storage.ReadAsync(path: "movies/avatar.mkv", ct: CancellationToken.None);

        result.Should().Equal(expected: payload, because: "bytes written must be exactly the bytes read back");
    }

    [Fact]
    public async Task OpenReadAsync_returns_stream_positioned_at_start()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "clip.mp4", content: [0xAA, 0xBB, 0xCC]);

        await using Stream stream = await storage.OpenReadAsync(path: "clip.mp4", ct: CancellationToken.None);
        byte[] buffer = new byte[3];
        int read = await stream.ReadAsync(buffer: buffer, cancellationToken: CancellationToken.None);

        read.Should().Be(expected: 3);
        buffer.Should().Equal(elements: [0xAA, 0xBB, 0xCC]);
    }

    [Fact]
    public async Task OpenWriteAsync_overwrite_false_throws_when_object_exists()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "existing.txt", content: [0x01]);

        Func<Task> act = async () =>
        {
            await using Stream s = await storage.OpenWriteAsync(
                path: "existing.txt",
                overwrite: false,
                ct: CancellationToken.None
            );
        };

        await act.Should()
            .ThrowAsync<IOException>(
                because: "overwrite:false must not silently clobber an existing object"
            );
    }

    // ------------------------------------------------------------------
    // ExistsAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ExistsAsync_true_for_file_and_directory_false_otherwise()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "a/file.txt", content: [0x01]);
        driver.SeedDirectory(path: "a/dir");

        (await storage.ExistsAsync(path: "a/file.txt", ct: CancellationToken.None)).Should().BeTrue();
        (await storage.ExistsAsync(path: "a/dir", ct: CancellationToken.None)).Should().BeTrue();
        (await storage.ExistsAsync(path: "a/missing", ct: CancellationToken.None)).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // DeleteAsync / DeleteDirectoryAsync — idempotent on missing
    // ------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_removes_existing_file_and_is_noop_when_missing()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "gone.txt", content: [0x01]);

        await storage.DeleteAsync(path: "gone.txt", ct: CancellationToken.None);
        driver.FileExists(path: "gone.txt").Should().BeFalse();

        Func<Task> act = async () => await storage.DeleteAsync(path: "gone.txt", ct: CancellationToken.None);
        await act.Should()
            .NotThrowAsync(because: "deleting an already-missing file must be a no-op, not an error");
    }

    [Fact]
    public async Task DeleteDirectoryAsync_removes_existing_and_is_noop_when_missing()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedDirectory(path: "old-lib");

        await storage.DeleteDirectoryAsync(path: "old-lib", recursive: true, ct: CancellationToken.None);
        driver.DirectoryExists(path: "old-lib").Should().BeFalse();

        Func<Task> act = async () =>
            await storage.DeleteDirectoryAsync(path: "old-lib", recursive: true, ct: CancellationToken.None);
        await act.Should().NotThrowAsync(because: "deleting an already-missing directory must be a no-op");
    }

    // ------------------------------------------------------------------
    // CreateDirectoryAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task CreateDirectoryAsync_delegates_to_driver()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        await storage.CreateDirectoryAsync(path: "new/nested/dir", ct: CancellationToken.None);

        driver.DirectoryExists(path: "new/nested/dir").Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // MoveAsync / CopyAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task MoveAsync_moves_bytes_and_removes_source()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "src.txt", content: [0x42]);

        await storage.MoveAsync(from: "src.txt", to: "dst.txt", ct: CancellationToken.None);

        driver.FileExists(path: "src.txt").Should().BeFalse(because: "source must be removed after move");
        (await storage.ReadAsync(path: "dst.txt", ct: CancellationToken.None)).Should().Equal(elements: 0x42);
    }

    [Fact]
    public async Task CopyAsync_duplicates_bytes_and_keeps_source()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "src.txt", content: [0x7A]);

        await storage.CopyAsync(from: "src.txt", to: "dst.txt", ct: CancellationToken.None);

        driver.FileExists(path: "src.txt").Should().BeTrue(because: "copy must keep the source");
        (await storage.ReadAsync(path: "dst.txt", ct: CancellationToken.None)).Should().Equal(elements: 0x7A);
    }

    // ------------------------------------------------------------------
    // SizeAsync / LastModifiedAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task SizeAsync_returns_byte_length()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "file.bin", content: new byte[128]);

        long size = await storage.SizeAsync(path: "file.bin", ct: CancellationToken.None);

        size.Should().Be(expected: 128);
    }

    [Fact]
    public async Task LastModifiedAsync_returns_utc_offset_with_zero_offset()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "file.bin", content: [0x01]);

        DateTimeOffset result = await storage.LastModifiedAsync(path: "file.bin", ct: CancellationToken.None);

        result.Offset.Should().Be(expected: TimeSpan.Zero, because: "remote drivers always report UTC");
    }

    // ------------------------------------------------------------------
    // ListAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ListAsync_yields_scope_relative_entries()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "season1/e01.mkv", content: new byte[10]);
        driver.SeedFile(path: "season1/e02.mkv", content: new byte[20]);

        List<StorageEntry> entries = [];
        await foreach (
            StorageEntry entry in storage.ListAsync(
                path: "season1",
                pattern: null,
                recursive: false,
                ct: CancellationToken.None
            )
        )
            entries.Add(item: entry);

        entries.Should().HaveCount(expected: 2);
        entries.Select(selector: e => e.Path).Should().Contain(expected: ["season1/e01.mkv", "season1/e02.mkv"]);
    }

    [Fact]
    public async Task ListAsync_honors_cancellation()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "a.txt", content: [0x01]);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Func<Task> act = async () =>
        {
            await foreach (
                StorageEntry _ in storage.ListAsync(path: "", pattern: null, recursive: true, ct: cts.Token)
            ) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------
    // HashAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task HashAsync_sha256_matches_known_digest()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "hello.txt", content: "hello"u8.ToArray());

        string hash = await storage.HashAsync(path: "hello.txt", algorithm: "SHA256", ct: CancellationToken.None);

        hash.Should()
            .Be(
                expected: "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
                because: "sha256('hello') is a well-known digest; a wrong result means the algorithm dispatch broke"
            );
    }

    // ------------------------------------------------------------------
    // AcquireLocalPathAsync (async) — stages to temp file, cleans up on dispose
    // ------------------------------------------------------------------

    [Fact]
    public async Task AcquireLocalPathAsync_stages_remote_object_to_temp_and_cleans_up()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        byte[] payload = [1, 2, 3, 4, 5];
        driver.SeedFile(path: "remote/video.mp4", content: payload);

        LocalPathLease lease = await storage.AcquireLocalPathAsync(
            path: "remote/video.mp4",
            ct: CancellationToken.None
        );
        try
        {
            File.Exists(path: lease.Path).Should().BeTrue(because: "lease must materialize a real local file");
            File.ReadAllBytes(path: lease.Path).Should().Equal(elements: payload);
        }
        finally
        {
            await lease.DisposeAsync();
        }

        File.Exists(path: lease.Path).Should().BeFalse(because: "dispose must delete the staged temp file");
    }

    // ------------------------------------------------------------------
    // Sync companions
    // ------------------------------------------------------------------

    [Fact]
    public void Exists_true_for_file_and_directory_false_otherwise()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: [0x01]);
        driver.SeedDirectory(path: "d");

        storage.Exists(path: "f.txt").Should().BeTrue();
        storage.Exists(path: "d").Should().BeTrue();
        storage.Exists(path: "missing").Should().BeFalse();
    }

    [Fact]
    public void SizeOrZero_returns_size_for_existing_and_zero_for_missing()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: new byte[42]);

        storage.SizeOrZero(path: "f.txt").Should().Be(expected: 42);
        storage.SizeOrZero(path: "missing.txt").Should().Be(expected: 0);
    }

    [Fact]
    public void Size_returns_driver_reported_size()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: new byte[7]);

        storage.Size(path: "f.txt").Should().Be(expected: 7);
    }

    [Fact]
    public void LastModified_returns_utc_offset()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: [0x01]);

        storage.LastModified(path: "f.txt").Offset.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public void CreateDirectory_delegates_to_driver()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        storage.CreateDirectory(path: "brand/new");

        driver.DirectoryExists(path: "brand/new").Should().BeTrue();
    }

    [Fact]
    public void Delete_removes_existing_and_is_noop_when_missing()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: [0x01]);

        storage.Delete(path: "f.txt");
        driver.FileExists(path: "f.txt").Should().BeFalse();

        Action act = () => storage.Delete(path: "f.txt");
        act.Should().NotThrow(because: "deleting an already-missing file is idempotent");
    }

    [Fact]
    public void DeleteDirectory_removes_existing_and_is_noop_when_missing()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedDirectory(path: "d");

        storage.DeleteDirectory(path: "d", recursive: false);
        driver.DirectoryExists(path: "d").Should().BeFalse();

        Action act = () => storage.DeleteDirectory(path: "d", recursive: false);
        act.Should().NotThrow(because: "deleting an already-missing directory is idempotent");
    }

    [Fact]
    public void Read_returns_full_content_synchronously()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: [0x9, 0x8, 0x7]);

        storage.Read(path: "f.txt").Should().Equal(elements: [0x9, 0x8, 0x7]);
    }

    [Fact]
    public void OpenRead_returns_a_readable_stream()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "f.txt", content: [0x1]);

        using Stream stream = storage.OpenRead(path: "f.txt");

        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public void OpenWrite_then_dispose_commits_bytes()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        using (Stream stream = storage.OpenWrite(path: "out.txt", overwrite: true))
            stream.Write(buffer: [0x5, 0x6], offset: 0, count: 2);

        driver.FileExists(path: "out.txt").Should().BeTrue();
    }

    [Fact]
    public void Write_writes_bytes_synchronously()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        storage.Write(path: "out.txt", bytes: [0x1, 0x2, 0x3]);

        driver.FileExists(path: "out.txt").Should().BeTrue();
        storage.Read(path: "out.txt").Should().Equal(elements: [0x1, 0x2, 0x3]);
    }

    [Fact]
    public void Move_moves_bytes_synchronously()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "a.txt", content: [0x9]);

        storage.Move(from: "a.txt", to: "b.txt");

        driver.FileExists(path: "a.txt").Should().BeFalse();
        storage.Read(path: "b.txt").Should().Equal(elements: 0x9);
    }

    [Fact]
    public void Copy_duplicates_bytes_synchronously()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "a.txt", content: [0x9]);

        storage.Copy(from: "a.txt", to: "b.txt");

        driver.FileExists(path: "a.txt").Should().BeTrue();
        storage.Read(path: "b.txt").Should().Equal(elements: 0x9);
    }

    [Fact]
    public void List_returns_entries_with_size_and_modified_time()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "dir/a.txt", content: new byte[3]);

        IReadOnlyList<StorageEntry> entries = storage.List(path: "dir", pattern: null, recursive: false);

        entries.Should().ContainSingle();
        entries[index: 0].SizeBytes.Should().Be(expected: 3);
    }

    [Fact]
    public void AcquireLocalPath_sync_stages_and_cleans_up()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        byte[] payload = [7, 7, 7];
        driver.SeedFile(path: "remote.bin", content: payload);

        LocalPathLease lease = storage.AcquireLocalPath(path: "remote.bin");
        try
        {
            File.ReadAllBytes(path: lease.Path).Should().Equal(elements: payload);
        }
        finally
        {
            lease.Dispose();
        }

        File.Exists(path: lease.Path)
            .Should()
            .BeFalse(because: "sync AcquireLocalPath must also clean up on dispose");
    }

    [Fact]
    public async Task ReadAllTextAsync_decodes_utf8_content()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedFile(path: "readme.txt", content: System.Text.Encoding.UTF8.GetBytes(s: "hello world"));

        string text = await storage.ReadAllTextAsync(path: "readme.txt", ct: CancellationToken.None);

        text.Should().Be(expected: "hello world");
    }

    [Fact]
    public async Task WriteAllTextAsync_writes_utf8_bytes()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        await storage.WriteAllTextAsync(path: "out.txt", contents: "some content", ct: CancellationToken.None);

        driver.FileExists(path: "out.txt").Should().BeTrue();
        string readBack = await storage.ReadAllTextAsync(path: "out.txt", ct: CancellationToken.None);
        readBack.Should().Be(expected: "some content");
    }

    [Fact]
    public async Task MoveDirectoryAsync_delegates_to_driver()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedDirectory(path: "old");

        await storage.MoveDirectoryAsync(from: "old", to: "new", ct: CancellationToken.None);

        driver.MoveDirectoryCallCount.Should().Be(expected: 1);
    }

    [Fact]
    public void MoveDirectory_sync_delegates_to_driver()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);
        driver.SeedDirectory(path: "old");

        storage.MoveDirectory(from: "old", to: "new");

        driver.MoveDirectoryCallCount.Should().Be(expected: 1);
    }

    // ------------------------------------------------------------------
    // Rule 6 — GetFullPath escape hatch is LocalStorage-only; RemoteStorage
    // does not override it, so the IStorage default must throw.
    // ------------------------------------------------------------------

    [Fact]
    public void GetFullPath_throws_NotSupportedException_for_remote_storage()
    {
        RemoteStorage storage = NewStorage(driver: out _);
        IStorage asInterface = storage;

        Action act = () => asInterface.GetFullPath(path: "anything.txt");

        act.Should()
            .Throw<NotSupportedException>(
                because: "GetFullPath escapes the abstraction and is only valid for LocalStorage; "
                         + "remote drivers have no meaningful local filesystem path"
            );
    }

    // ------------------------------------------------------------------
    // CombinePath / DirectorySeparator default-interface members, exercised
    // through the facade exactly like production S3/SMB/WebDAV drivers do
    // (none of those override CombinePath or DirectorySeparator either).
    // ------------------------------------------------------------------

    [Fact]
    public void DirectorySeparator_defaults_to_forward_slash_for_remote_drivers()
    {
        IStorage storage = NewStorage(driver: out _);

        storage
            .DirectorySeparator.Should()
            .Be(expected: '/', because: "remote drivers speak '/' regardless of host OS");
    }

    [Fact]
    public void CombinePath_default_implementation_joins_with_forward_slash()
    {
        IStorage storage = NewStorage(driver: out _);

        storage.CombinePath(parent: "season1", child: "e01.mkv").Should().Be(expected: "season1/e01.mkv");
    }

    [Fact]
    public void CombinePath_default_implementation_trims_redundant_separators()
    {
        IStorage storage = NewStorage(driver: out _);

        storage.CombinePath(parent: "season1/", child: "/e01.mkv").Should().Be(expected: "season1/e01.mkv");
    }

    [Fact]
    public void CombinePath_default_implementation_handles_empty_segments()
    {
        IStorage storage = NewStorage(driver: out _);

        storage.CombinePath(parent: "", child: "child").Should().Be(expected: "child");
        storage.CombinePath(parent: "parent", child: "").Should().Be(expected: "parent");
    }

    // ------------------------------------------------------------------
    // BackendLabel default fallback (GetType().Name) — no production driver
    // overrides it away from a real label, but the fallback is a documented
    // contract for future/test drivers and must resolve to the type name.
    // ------------------------------------------------------------------

    [Fact]
    public void BackendLabel_defaults_to_the_concrete_type_name()
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        storage.Driver.BackendLabel.Should().Be(expected: nameof(InMemoryStorageDriver));
    }

    // ------------------------------------------------------------------
    // TryGetPresignedUrlAsync default fallback — null for drivers that don't
    // support presigning (local / NFS / WebDAV in production).
    // ------------------------------------------------------------------

    [Fact]
    public async Task TryGetPresignedUrlAsync_defaults_to_null_when_driver_does_not_support_it()
    {
        RemoteStorage storage = NewStorage(driver: out _);
        IStorage asInterface = storage;

        Uri? result = await asInterface.TryGetPresignedUrlAsync(
            path: "f.txt",
            ttl: TimeSpan.FromMinutes(minutes: 5),
            ct: CancellationToken.None
        );

        result.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // IStorageDriver.AcquireLocalPathAsync default-interface-member — used
    // directly (not via RemoteStorage, which has its own override) by
    // FfProbeService for every driver EXCEPT LocalStorageDriver (S3/NFS/SMB/
    // WebDAV all fall through to this default). Must stage to a real temp
    // file under StoragePaths.TempRoot and clean it up on lease dispose.
    // ------------------------------------------------------------------

    [Fact]
    public async Task IStorageDriver_default_AcquireLocalPathAsync_stages_and_cleans_up()
    {
        InMemoryStorageDriver driver = new();
        byte[] payload = [9, 8, 7, 6];
        driver.SeedFile(path: "clip.mkv", content: payload);
        IStorageDriver asInterface = driver;

        LocalPathLease lease = await asInterface.AcquireLocalPathAsync(
            path: "clip.mkv",
            ct: CancellationToken.None
        );
        try
        {
            Path.IsPathRooted(path: lease.Path)
                .Should()
                .BeTrue(because: "the default implementation must materialize a real OS path");
            File.ReadAllBytes(path: lease.Path).Should().Equal(elements: payload);
        }
        finally
        {
            await lease.DisposeAsync();
        }

        File.Exists(path: lease.Path)
            .Should()
            .BeFalse(because: "the default implementation's cleanup callback must delete the staged file");
    }

    // ------------------------------------------------------------------
    // Absolute-path rejection happens BEFORE the driver is ever touched —
    // regression guard for the V() gate on every entry point not already
    // covered by RemoteStoragePathGuardTests.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(data: "/abs/path")]
    [InlineData(data: @"C:\abs\path")]
    public void Move_rejects_absolute_source_or_destination(string absolute)
    {
        RemoteStorage storage = NewStorage(driver: out InMemoryStorageDriver driver);

        Action act = () => storage.Move(from: absolute, to: "dest.txt");

        act.Should().Throw<StoragePathNotAllowedException>();
        driver
            .FileExists(path: "dest.txt")
            .Should()
            .BeFalse(because: "driver must never be reached for a rejected path");
    }
}
