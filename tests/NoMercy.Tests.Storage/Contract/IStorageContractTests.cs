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
    [Trait("Category", "Unit")]
    public async Task List_empty_string_does_not_throw()
    {
        IStorage storage = CreateStorage();
        try
        {
            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "",
                    "*",
                    recursive: false,
                    CancellationToken.None
                )
            )
                entries.Add(e);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task List_null_pattern_does_not_throw()
    {
        IStorage storage = CreateStorage();
        try
        {
            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "",
                    null,
                    recursive: false,
                    CancellationToken.None
                )
            )
                entries.Add(e);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public virtual async Task Exists_empty_string_root_returns_true_when_root_is_directory()
    {
        IStorage storage = CreateStorage();
        try
        {
            bool exists = await storage.ExistsAsync("", CancellationToken.None);
            exists.Should().BeTrue("the storage root must always exist");
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
    [Trait("Category", "Unit")]
    public async Task Exists_backslash_and_forward_slash_are_equivalent()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] data = Payload(10);
            await SeedFile("foo/bar.bin", data);

            bool withForward = await storage.ExistsAsync("foo/bar.bin", CancellationToken.None);
            bool withBackslash = await storage.ExistsAsync("foo\\bar.bin", CancellationToken.None);

            withForward.Should().BeTrue();
            withBackslash
                .Should()
                .Be(withForward, "backslash and forward slash must normalize identically");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task Exists_double_slash_normalizes_correctly()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] data = Payload(11);
            await SeedFile("foo/bar.bin", data);

            bool withDouble = await storage.ExistsAsync("foo//bar.bin", CancellationToken.None);
            bool withSingle = await storage.ExistsAsync("foo/bar.bin", CancellationToken.None);

            withSingle.Should().BeTrue();
            withDouble.Should().Be(withSingle, "double slashes must be collapsed");
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
    [Trait("Category", "Unit")]
    public async Task WriteAsync_then_ReadAsync_round_trips_bytes()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(42);
            await storage.WriteAsync("roundtrip.bin", payload, CancellationToken.None);
            byte[] result = await storage.ReadAsync("roundtrip.bin", CancellationToken.None);
            result.Should().Equal(payload);
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_then_ExistsAsync_returns_true()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("exists-check.bin", Payload(1), CancellationToken.None);
            bool exists = await storage.ExistsAsync("exists-check.bin", CancellationToken.None);
            exists.Should().BeTrue();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task WriteAsync_propagates_to_backend()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(7);
            await storage.WriteAsync("backend-check.bin", payload, CancellationToken.None);
            bool landed = await BackendHasFile("backend-check.bin");
            landed.Should().BeTrue("Write must persist to the backend store");
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
    [Trait("Category", "Unit")]
    public async Task WriteAsync_nested_path_creates_intermediate_directories()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(3);
            await storage.WriteAsync("a/b/c.bin", payload, CancellationToken.None);

            byte[] result = await storage.ReadAsync("a/b/c.bin", CancellationToken.None);
            result.Should().Equal(payload);
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
    [Trait("Category", "Unit")]
    public async Task WriteAsync_encoder_shape_path_round_trips()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(99);
            string encoderPath = "Black Butler (2008)/Season 05/episode.m3u8";
            await storage.WriteAsync(encoderPath, payload, CancellationToken.None);

            byte[] result = await storage.ReadAsync(encoderPath, CancellationToken.None);
            result.Should().Equal(payload);

            bool exists = await storage.ExistsAsync(encoderPath, CancellationToken.None);
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
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_removes_file_and_exists_becomes_false()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("del-test.bin", Payload(5), CancellationToken.None);
            await storage.DeleteAsync("del-test.bin", CancellationToken.None);

            bool exists = await storage.ExistsAsync("del-test.bin", CancellationToken.None);
            exists.Should().BeFalse();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task DeleteAsync_is_idempotent_on_missing_file()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("idempotent-del.bin", Payload(6), CancellationToken.None);
            await storage.DeleteAsync("idempotent-del.bin", CancellationToken.None);

            Func<Task> secondDelete = () =>
                storage.DeleteAsync("idempotent-del.bin", CancellationToken.None);
            await secondDelete.Should().NotThrowAsync("second delete must be a no-op");
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
    [Trait("Category", "Unit")]
    public async Task MoveAsync_source_gone_destination_exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(20);
            await storage.WriteAsync("move-src.bin", payload, CancellationToken.None);
            await storage.MoveAsync("move-src.bin", "move-dst.bin", CancellationToken.None);

            bool srcExists = await storage.ExistsAsync("move-src.bin", CancellationToken.None);
            bool dstExists = await storage.ExistsAsync("move-dst.bin", CancellationToken.None);

            srcExists.Should().BeFalse("source must be removed after move");
            dstExists.Should().BeTrue("destination must exist after move");
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task MoveAsync_across_directories()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(21);
            await storage.WriteAsync("dir-a/src.bin", payload, CancellationToken.None);
            await storage.MoveAsync("dir-a/src.bin", "dir-b/dst.bin", CancellationToken.None);

            bool srcExists = await storage.ExistsAsync("dir-a/src.bin", CancellationToken.None);
            bool dstExists = await storage.ExistsAsync("dir-b/dst.bin", CancellationToken.None);

            srcExists.Should().BeFalse();
            dstExists.Should().BeTrue();
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public async Task MoveAsync_content_is_preserved()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(22);
            await storage.WriteAsync("mv-content-src.bin", payload, CancellationToken.None);
            await storage.MoveAsync(
                "mv-content-src.bin",
                "mv-content-dst.bin",
                CancellationToken.None
            );

            byte[] result = await storage.ReadAsync("mv-content-dst.bin", CancellationToken.None);
            result.Should().Equal(payload);
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
    [Trait("Category", "Unit")]
    public async Task SizeAsync_returns_written_byte_count()
    {
        IStorage storage = CreateStorage();
        try
        {
            byte[] payload = Payload(1, 512);
            await storage.WriteAsync("sized.bin", payload, CancellationToken.None);
            long size = await storage.SizeAsync("sized.bin", CancellationToken.None);
            size.Should().Be(payload.Length);
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
    [Trait("Category", "Unit")]
    public virtual async Task LastModifiedAsync_is_recent_after_write()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("stamped.bin", Payload(2), CancellationToken.None);
            DateTimeOffset stamp = await storage.LastModifiedAsync(
                "stamped.bin",
                CancellationToken.None
            );

            (DateTimeOffset.UtcNow - stamp)
                .Should()
                .BeLessThan(TimeSpan.FromMinutes(1), "last-modified must be within 60s of write");
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
    [Trait("Category", "Unit")]
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
                        "nonexistent-dir-xyz",
                        "*",
                        recursive: false,
                        CancellationToken.None
                    )
                )
                    entries.Add(e);
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
    [Trait("Category", "Unit")]
    public virtual async Task List_with_pattern_filters_by_extension()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("filter/a.bin", Payload(1), CancellationToken.None);
            await storage.WriteAsync("filter/b.bin", Payload(2), CancellationToken.None);
            await storage.WriteAsync("filter/c.txt", Payload(3), CancellationToken.None);

            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "filter",
                    "*.bin",
                    recursive: false,
                    CancellationToken.None
                )
            )
                entries.Add(e);

            entries.Should().HaveCount(2, "only .bin files should match *.bin");
            entries
                .Should()
                .NotContain(e => e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
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
    [Trait("Category", "Unit")]
    public virtual async Task List_flat_does_not_see_subdir_contents()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("flat/top.bin", Payload(1), CancellationToken.None);
            await storage.WriteAsync("flat/sub/deep.bin", Payload(2), CancellationToken.None);

            List<StorageEntry> flat = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "flat",
                    "*",
                    recursive: false,
                    CancellationToken.None
                )
            )
                flat.Add(e);

            flat.Should()
                .NotContain(
                    e => e.Path.Contains("deep"),
                    "flat list must not recurse into subdirs"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait("Category", "Unit")]
    public virtual async Task List_recursive_sees_subdir_contents()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("rec/top.bin", Payload(1), CancellationToken.None);
            await storage.WriteAsync("rec/sub/deep.bin", Payload(2), CancellationToken.None);

            List<StorageEntry> recursive = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    "rec",
                    "*",
                    recursive: true,
                    CancellationToken.None
                )
            )
                recursive.Add(e);

            recursive
                .Should()
                .Contain(
                    e => e.Path.Contains("deep"),
                    "recursive list must include subdirectory contents"
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
    [Trait("Category", "Unit")]
    public virtual async Task Exists_null_byte_in_path_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync("foo\0bar", CancellationToken.None);
            await act.Should().ThrowAsync<Exception>("null bytes in paths must be rejected");
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
    [Trait("Category", "Unit")]
    public virtual async Task Exists_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync("../escape", CancellationToken.None);
            await act.Should().ThrowAsync<Exception>("'..' traversal paths must be rejected");
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
    [Trait("Category", "Unit")]
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
                result = await storage.ExistsAsync("/abs/path/escape", CancellationToken.None);
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
                    "absolute paths must be rejected (throw) or return false, never silently resolve"
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
    [Trait("Category", "Unit")]
    public async Task Concurrent_ExistsAsync_calls_return_correctly()
    {
        IStorage storage = CreateStorage();
        try
        {
            await storage.WriteAsync("concurrent-read.bin", Payload(55), CancellationToken.None);

            Task<bool>[] tasks = new Task<bool>[8];
            for (int i = 0; i < 8; i++)
                tasks[i] = storage.ExistsAsync("concurrent-read.bin", CancellationToken.None);

            bool[] results = await Task.WhenAll(tasks);
            results
                .Should()
                .AllBeEquivalentTo(true, "all concurrent Exists calls must return true");
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
    [Trait("Category", "Unit")]
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
                    $"concurrent/file-{index}.bin",
                    Payload(index),
                    CancellationToken.None
                );
            }
            await Task.WhenAll(writes);

            for (int i = 0; i < 8; i++)
            {
                bool exists = await storage.ExistsAsync(
                    $"concurrent/file-{i}.bin",
                    CancellationToken.None
                );
                exists.Should().BeTrue($"file-{i}.bin must exist after concurrent write");
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }
}
