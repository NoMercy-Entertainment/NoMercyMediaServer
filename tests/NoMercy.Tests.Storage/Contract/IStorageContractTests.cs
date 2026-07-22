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

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Abstract cross-driver contract suite for <see cref="IStorage"/>.
/// Every driver that implements IStorage must subclass this and provide
/// the five fixture hooks below. Tests run against the subclass-supplied
/// storage instance to prove uniform behavior regardless of backend.
///
/// Design decisions:
///   - "Absolute path" rejection tests document the expected exception as
///     <see cref="StoragePathNotAllowedException"/> or <see cref="ArgumentException"/>.
///     LocalStorage enforces this through StoragePathGuard; RemoteStorage has no guard
///     so the NFS suite marks those tests as explicit known-failures via Skip.
///   - List tests rely on the fake's ability to enumerate directory entries.
///     FaultyLibNfs.ReadDir always returns IntPtr.Zero, so NFS List tests will
///     return empty collections. NfsStorageContractTests skips those assertions.
/// </summary>
public abstract class IStorageContractTests
{
    // -----------------------------------------------------------------------
    // Optional async lifecycle hooks for subclasses that spin up containers.
    // Default implementations are no-ops so simple (non-container) subclasses
    // like LocalStorage and NfsStorage do not need to implement them.
    // -----------------------------------------------------------------------

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual Task DisposeAsync() => Task.CompletedTask;

    // -----------------------------------------------------------------------
    // Optional Docker-availability gate. Integration subclasses override to
    // return their real availability flag; unit-only subclasses leave it true.
    // -----------------------------------------------------------------------

    protected virtual bool DockerAvailable => true;

    // -----------------------------------------------------------------------
    // Fixture hooks — subclasses must implement
    // -----------------------------------------------------------------------

    /// <summary>Returns a fresh, clean storage instance for each test.</summary>
    protected abstract IStorage CreateStorage();

    /// <summary>
    /// Populates the backend so the file at <paramref name="relativePath"/> exists
    /// with <paramref name="content"/> before the storage under test is exercised.
    /// Path uses forward-slash separators, no leading slash.
    /// </summary>
    protected abstract Task SeedFile(string relativePath, byte[] content);

    /// <summary>
    /// Ensures <paramref name="relativePath"/> exists as a directory in the backend.
    /// Path uses forward-slash separators, no leading slash.
    /// </summary>
    protected abstract Task SeedDirectory(string relativePath);

    /// <summary>
    /// Returns true when the backend (checked independently of IStorage)
    /// contains a file at <paramref name="relativePath"/>.
    /// Used to verify that WriteAsync actually landed something.
    /// </summary>
    protected abstract Task<bool> BackendHasFile(string relativePath);

    /// <summary>Cleans up any backend state after each test.</summary>
    protected abstract Task DisposeStorage();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static byte[] Payload(int seed = 1, int length = 16)
    {
        byte[] data = new byte[length];
        for (int i = 0; i < length; i++)
            data[i] = (byte)((seed + i) % 256);
        return data;
    }

    // -----------------------------------------------------------------------
    // Scope semantics
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task List_empty_string_does_not_throw()
    {
        IStorage storage = CreateStorage();
        try
        {
            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "",
                    pattern: "*",
                    recursive: false,
                    ct: CancellationToken.None
                )
            )
                entries.Add(item: e);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task List_null_pattern_does_not_throw()
    {
        IStorage storage = CreateStorage();
        try
        {
            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "",
                    pattern: null,
                    recursive: false,
                    ct: CancellationToken.None
                )
            )
                entries.Add(item: e);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task Exists_empty_string_root_returns_true_when_root_is_directory()
    {
        IStorage storage = CreateStorage();
        try
        {
            bool exists = await storage.ExistsAsync(path: "", ct: CancellationToken.None);
            exists.Should().BeTrue(because: "the storage root must always exist");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Separator normalization
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Exists_backslash_and_forward_slash_are_equivalent()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] data = Payload(seed: 10);
            await SeedFile(relativePath: "foo/bar.bin", content: data);

            bool withForward = await storage.ExistsAsync(path: "foo/bar.bin", ct: CancellationToken.None);
            bool withBackslash = await storage.ExistsAsync(path: "foo\\bar.bin", ct: CancellationToken.None);

            withForward.Should().BeTrue();
            withBackslash
                .Should()
                .Be(expected: withForward, because: "backslash and forward slash must normalize identically");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Exists_double_slash_normalizes_correctly()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] data = Payload(seed: 11);
            await SeedFile(relativePath: "foo/bar.bin", content: data);

            bool withDouble = await storage.ExistsAsync(path: "foo//bar.bin", ct: CancellationToken.None);
            bool withSingle = await storage.ExistsAsync(path: "foo/bar.bin", ct: CancellationToken.None);

            withSingle.Should().BeTrue();
            withDouble.Should().Be(expected: withSingle, because: "double slashes must be collapsed");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Round-trip: write → read → exists
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task WriteAsync_then_ReadAsync_round_trips_bytes()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 42);
            await storage.WriteAsync(path: "roundtrip.bin", bytes: payload, ct: CancellationToken.None);
            byte[] result = await storage.ReadAsync(path: "roundtrip.bin", ct: CancellationToken.None);
            result.Should().Equal(elements: payload);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task WriteAsync_then_ExistsAsync_returns_true()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "exists-check.bin", bytes: Payload(seed: 1), ct: CancellationToken.None);
            bool exists = await storage.ExistsAsync(path: "exists-check.bin", ct: CancellationToken.None);
            exists.Should().BeTrue();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task WriteAsync_propagates_to_backend()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 7);
            await storage.WriteAsync(path: "backend-check.bin", bytes: payload, ct: CancellationToken.None);
            bool landed = await BackendHasFile(relativePath: "backend-check.bin");
            landed.Should().BeTrue(because: "Write must persist to the backend store");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Round-trip with subdirectories
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task WriteAsync_nested_path_creates_intermediate_directories()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 3);
            await storage.WriteAsync(path: "a/b/c.bin", bytes: payload, ct: CancellationToken.None);

            byte[] result = await storage.ReadAsync(path: "a/b/c.bin", ct: CancellationToken.None);
            result.Should().Equal(elements: payload);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Encoder-shape paths (parens, spaces, dots)
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task WriteAsync_encoder_shape_path_round_trips()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 99);
            string encoderPath = "Black Butler (2008)/Season 05/episode.m3u8";
            await storage.WriteAsync(path: encoderPath, bytes: payload, ct: CancellationToken.None);

            byte[] result = await storage.ReadAsync(path: encoderPath, ct: CancellationToken.None);
            result.Should().Equal(elements: payload);

            bool exists = await storage.ExistsAsync(path: encoderPath, ct: CancellationToken.None);
            exists.Should().BeTrue();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Delete
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task DeleteAsync_removes_file_and_exists_becomes_false()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "del-test.bin", bytes: Payload(seed: 5), ct: CancellationToken.None);
            await storage.DeleteAsync(path: "del-test.bin", ct: CancellationToken.None);

            bool exists = await storage.ExistsAsync(path: "del-test.bin", ct: CancellationToken.None);
            exists.Should().BeFalse();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task DeleteAsync_is_idempotent_on_missing_file()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "idempotent-del.bin", bytes: Payload(seed: 6), ct: CancellationToken.None);
            await storage.DeleteAsync(path: "idempotent-del.bin", ct: CancellationToken.None);

            Func<Task> secondDelete = () =>
                storage.DeleteAsync(path: "idempotent-del.bin", ct: CancellationToken.None);
            await secondDelete.Should().NotThrowAsync(because: "second delete must be a no-op");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Move
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task MoveAsync_source_gone_destination_exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 20);
            await storage.WriteAsync(path: "move-src.bin", bytes: payload, ct: CancellationToken.None);
            await storage.MoveAsync(from: "move-src.bin", to: "move-dst.bin", ct: CancellationToken.None);

            bool srcExists = await storage.ExistsAsync(path: "move-src.bin", ct: CancellationToken.None);
            bool dstExists = await storage.ExistsAsync(path: "move-dst.bin", ct: CancellationToken.None);

            srcExists.Should().BeFalse(because: "source must be removed after move");
            dstExists.Should().BeTrue(because: "destination must exist after move");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task MoveAsync_across_directories()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 21);
            await storage.WriteAsync(path: "dir-a/src.bin", bytes: payload, ct: CancellationToken.None);
            await storage.MoveAsync(from: "dir-a/src.bin", to: "dir-b/dst.bin", ct: CancellationToken.None);

            bool srcExists = await storage.ExistsAsync(path: "dir-a/src.bin", ct: CancellationToken.None);
            bool dstExists = await storage.ExistsAsync(path: "dir-b/dst.bin", ct: CancellationToken.None);

            srcExists.Should().BeFalse();
            dstExists.Should().BeTrue();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task MoveAsync_content_is_preserved()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 22);
            await storage.WriteAsync(path: "mv-content-src.bin", bytes: payload, ct: CancellationToken.None);
            await storage.MoveAsync(
                from: "mv-content-src.bin",
                to: "mv-content-dst.bin",
                ct: CancellationToken.None
            );

            byte[] result = await storage.ReadAsync(path: "mv-content-dst.bin", ct: CancellationToken.None);
            result.Should().Equal(elements: payload);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Size
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task SizeAsync_returns_written_byte_count()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(seed: 1, length: 512);
            await storage.WriteAsync(path: "sized.bin", bytes: payload, ct: CancellationToken.None);
            long size = await storage.SizeAsync(path: "sized.bin", ct: CancellationToken.None);
            size.Should().Be(expected: payload.Length);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Last-modified timestamp
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task LastModifiedAsync_is_recent_after_write()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "stamped.bin", bytes: Payload(seed: 2), ct: CancellationToken.None);
            DateTimeOffset stamp = await storage.LastModifiedAsync(
                path: "stamped.bin",
                ct: CancellationToken.None
            );

            (DateTimeOffset.UtcNow - stamp)
                .Should()
                .BeLessThan(expected: TimeSpan.FromMinutes(minutes: 1), because: "last-modified must be within 60s of write");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // List — empty result on missing directory is success (no throw)
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task List_nonexistent_directory_returns_empty_not_throw()
    {
        IStorage storage = CreateStorage();
        try
        {
            List<StorageEntry> entries = [];
            Func<Task> act = async () =>
            {
                await foreach (
                    StorageEntry e in storage.ListAsync(
                        path: "nonexistent-dir-xyz",
                        pattern: "*",
                        recursive: false,
                        ct: CancellationToken.None
                    )
                )
                    entries.Add(item: e);
            };

            await act.Should().NotThrowAsync();
            entries.Should().BeEmpty();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // List — pattern filter
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task List_with_pattern_filters_by_extension()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "filter/a.bin", bytes: Payload(seed: 1), ct: CancellationToken.None);
            await storage.WriteAsync(path: "filter/b.bin", bytes: Payload(seed: 2), ct: CancellationToken.None);
            await storage.WriteAsync(path: "filter/c.txt", bytes: Payload(seed: 3), ct: CancellationToken.None);

            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "filter",
                    pattern: "*.bin",
                    recursive: false,
                    ct: CancellationToken.None
                )
            )
                entries.Add(item: e);

            entries.Should().HaveCount(expected: 2, because: "only .bin files should match *.bin");
            entries
                .Should()
                .NotContain(predicate: e => e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // List — recursive vs flat
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task List_flat_does_not_see_subdir_contents()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "flat/top.bin", bytes: Payload(seed: 1), ct: CancellationToken.None);
            await storage.WriteAsync(path: "flat/sub/deep.bin", bytes: Payload(seed: 2), ct: CancellationToken.None);

            List<StorageEntry> flat = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "flat",
                    pattern: "*",
                    recursive: false,
                    ct: CancellationToken.None
                )
            )
                flat.Add(item: e);

            flat.Should()
                .NotContain(
                    predicate: e => e.Path.Contains("deep"),
                    because: "flat list must not recurse into subdirs"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task List_recursive_sees_subdir_contents()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "rec/top.bin", bytes: Payload(seed: 1), ct: CancellationToken.None);
            await storage.WriteAsync(path: "rec/sub/deep.bin", bytes: Payload(seed: 2), ct: CancellationToken.None);

            List<StorageEntry> recursive = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "rec",
                    pattern: "*",
                    recursive: true,
                    ct: CancellationToken.None
                )
            )
                recursive.Add(item: e);

            recursive
                .Should()
                .Contain(
                    predicate: e => e.Path.Contains("deep"),
                    because: "recursive list must include subdirectory contents"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Rejection — null byte
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task Exists_null_byte_in_path_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync(path: "foo\0bar", ct: CancellationToken.None);
            await act.Should().ThrowAsync<Exception>(because: "null bytes in paths must be rejected");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Rejection — ".." traversal
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task Exists_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync(path: "../escape", ct: CancellationToken.None);
            await act.Should().ThrowAsync<Exception>(because: "'..' traversal paths must be rejected");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Rejection — absolute path
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public virtual async Task Exists_absolute_path_is_rejected_or_returns_false()
    {
        IStorage storage = CreateStorage();
        try
        {
            // Contract: absolute paths must not silently succeed against
            // arbitrary filesystem locations. Acceptable outcomes are:
            //   a) StoragePathNotAllowedException (LocalStorage with guard)
            //   b) returns false (NFS: driver normalizes /abs/path as NFS-relative)
            // Both outcomes are documented here. Subclasses may override to be stricter.
            bool threw = false;
            bool result = false;
            try
            {
                result = await storage.ExistsAsync(path: "/abs/path/escape", ct: CancellationToken.None);
            }
            catch (StoragePathNotAllowedException)
            {
                threw = true;
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            bool acceptable = threw || !result;
            acceptable
                .Should()
                .BeTrue(
                    because: "absolute paths must be rejected (throw) or return false, never silently resolve"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Concurrent reads — stability check
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Concurrent_ExistsAsync_calls_return_correctly()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync(path: "concurrent-read.bin", bytes: Payload(seed: 55), ct: CancellationToken.None);

            Task<bool>[] tasks = new Task<bool>[8];
            for (int i = 0; i < 8; i++)
                tasks[i] = storage.ExistsAsync(path: "concurrent-read.bin", ct: CancellationToken.None);

            bool[] results = await Task.WhenAll(tasks: tasks);
            results
                .Should()
                .AllBeEquivalentTo(expectation: true, because: "all concurrent Exists calls must return true");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Concurrent writes — all land
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task Concurrent_WriteAsync_to_different_paths_all_land()
    {
        IStorage storage = CreateStorage();
        try
        {
            Task[] writes = new Task[8];
            for (int i = 0; i < 8; i++)
            {
                int index = i;
                writes[i] = storage.WriteAsync(
                    path: $"concurrent/file-{index}.bin",
                    bytes: Payload(seed: index),
                    ct: CancellationToken.None
                );
            }
            await Task.WhenAll(tasks: writes);

            for (int i = 0; i < 8; i++)
            {
                bool exists = await storage.ExistsAsync(
                    path: $"concurrent/file-{i}.bin",
                    ct: CancellationToken.None
                );
                exists.Should().BeTrue(because: $"file-{i}.bin must exist after concurrent write");
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }
}
