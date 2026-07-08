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

using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Storage;
using NoMercy.Encoder.Bundle;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Bundle;

// ---------------------------------------------------------------------------
// In-memory IStorage test double for rename tests
// ---------------------------------------------------------------------------

internal sealed class RenameTestStorage : IStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(
        StringComparer.OrdinalIgnoreCase
    );

    public void Seed(string path, byte[] bytes) => _files[path] = bytes;

    public string? ReadString(string path) =>
        _files.TryGetValue(path, out byte[]? bytes) ? Encoding.UTF8.GetString(bytes) : null;

    public IReadOnlyList<string> AllPaths() => [.. _files.Keys];

    // IStorage members used by BundleSlugRenamer

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(
            _files.ContainsKey(path)
                || _files.Keys.Any(k =>
                    k.StartsWith(path.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
                )
        );

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct)
    {
        string fromPrefix = from.TrimEnd('/') + "/";
        string toPrefix = to.TrimEnd('/') + "/";

        List<string> keys =
        [
            .. _files.Keys.Where(k => k.StartsWith(fromPrefix, StringComparison.OrdinalIgnoreCase)),
        ];
        foreach (string key in keys)
        {
            byte[] bytes = _files[key];
            string newKey = toPrefix + key[fromPrefix.Length..];
            _files[newKey] = bytes;
            _files.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct)
    {
        if (!_files.TryGetValue(path, out byte[]? bytes))
            throw new FileNotFoundException($"RenameTestStorage: '{path}' not found");
        return Task.FromResult(Encoding.UTF8.GetString(bytes));
    }

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct)
    {
        _files[path] = Encoding.UTF8.GetBytes(contents);
        return Task.CompletedTask;
    }

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        _files[path] = bytes;
        return Task.CompletedTask;
    }

    // --- Remaining IStorage members not used by BundleSlugRenamer ---

    public Task<byte[]> ReadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task DeleteAsync(string path, CancellationToken ct) => throw new NotSupportedException();

    public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task CreateDirectoryAsync(string path, CancellationToken ct) => Task.CompletedTask;

    public Task MoveAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task CopyAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<long> SizeAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<DateTimeOffset> LastModifiedAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<StorageEntry> ListAsync(
        string path,
        string? pattern,
        bool recursive,
        CancellationToken ct
    ) => throw new NotSupportedException();

    public Task<string> HashAsync(string path, string algorithm, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<LocalPathLease> AcquireLocalPathAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public bool Exists(string path) => _files.ContainsKey(path);

    public long SizeOrZero(string path) => 0;

    public long Size(string path) => throw new NotSupportedException();

    public DateTimeOffset LastModified(string path) => DateTimeOffset.UtcNow;

    public void CreateDirectory(string path) { }

    public void Delete(string path) => throw new NotSupportedException();

    public void DeleteDirectory(string path, bool recursive) => throw new NotSupportedException();

    public byte[] Read(string path) => throw new NotSupportedException();

    public Stream OpenRead(string path) => throw new NotSupportedException();

    public Stream OpenWrite(string path, bool overwrite) => throw new NotSupportedException();

    public void Write(string path, byte[] bytes) => _files[path] = bytes;

    public void Move(string from, string to) => throw new NotSupportedException();

    public void Copy(string from, string to) => throw new NotSupportedException();

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive) =>
        throw new NotSupportedException();

    public LocalPathLease AcquireLocalPath(string path) => throw new NotSupportedException();

    public void MoveDirectory(string from, string to) => throw new NotSupportedException();

    public IStorageDriver Driver => throw new NotSupportedException();
}

// ---------------------------------------------------------------------------
// Fake IStorageFactory that always returns the same storage instance
// ---------------------------------------------------------------------------

internal sealed class FixedStorageFactory(IStorage storage) : IStorageFactory
{
    public IStorage For(Ulid folderId, Ulid driverId, string subPath) => storage;

    public void Invalidate(Ulid folderId) { }

    public void InvalidateAll() { }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

internal static class RenameTestHelpers
{
    private static readonly Ulid FolderId = Ulid.NewUlid();
    private static readonly Ulid DriverId = Ulid.NewUlid();

    public static MediaContext BuildInMemoryContext(string folderPath = "/fake/library")
    {
        DbContextOptions<MediaContext> opts = new DbContextOptionsBuilder<MediaContext>()
            .UseInMemoryDatabase(databaseName: Ulid.NewUlid().ToString())
            .Options;

        MediaContext ctx = new(opts);

        ctx.Drivers.Add(
            new()
            {
                Id = DriverId,
                Name = "local",
                Type = "local",
                Config = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }
        );

        ctx.Folders.Add(
            new()
            {
                Id = FolderId,
                Path = folderPath,
                DriverId = DriverId,
            }
        );

        ctx.SaveChanges();
        return ctx;
    }

    public static string BuildManifestJson(string presetSlug) =>
        JsonConvert.SerializeObject(
            new JObject
            {
                ["version"] = 1,
                ["preset_slug"] = presetSlug,
                ["preset_name"] = "Old Name",
                ["media_key"] = "mfa",
                ["files"] = new JArray("mfa_master.m3u8", "video/1080p/mfa_1080p_00001.m4s"),
            },
            Formatting.Indented
        );

    public static BundleSlugRenamer BuildRenamer(
        Dictionary<string, string> slugMap,
        IStorage storage,
        MediaContext context
    )
    {
        FixedStorageFactory factory = new(storage);
        return new(
            slugMap,
            factory,
            context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<BundleSlugRenamer>.Instance
        );
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class BundleSlugRenamerTests
{
    [Fact]
    public async Task HappyPath_RenamesDirectoryAndPatchesManifest()
    {
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        // Seed the old bundle directory with a manifest and a segment file.
        string oldManifestPath = "encodes/old-slug/manifest.json";
        string oldSegmentPath = "encodes/old-slug/video/1080p/mfa_1080p_00001.m4s";
        storage.Seed(
            oldManifestPath,
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("old-slug"))
        );
        storage.Seed(oldSegmentPath, [0x01, 0x02]);

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        await renamer.RunAsync();

        // Old paths must be gone.
        storage.AllPaths().Should().NotContain(oldManifestPath);
        storage.AllPaths().Should().NotContain(oldSegmentPath);

        // New paths must exist.
        string newManifestPath = "encodes/new-slug/manifest.json";
        string newSegmentPath = "encodes/new-slug/video/1080p/mfa_1080p_00001.m4s";
        storage.AllPaths().Should().Contain(newManifestPath);
        storage.AllPaths().Should().Contain(newSegmentPath);

        // Manifest must have the patched preset_slug.
        string? json = storage.ReadString(newManifestPath);
        json.Should().NotBeNull();
        JObject? manifest = JsonConvert.DeserializeObject<JObject>(json!);
        manifest!["preset_slug"]!.Value<string>().Should().Be("new-slug");
    }

    [Fact]
    public async Task Idempotent_SecondRunIsNoOp()
    {
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        storage.Seed(
            "encodes/old-slug/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("old-slug"))
        );
        storage.Seed("encodes/old-slug/segment.m4s", [0x01]);

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        // First run renames the directory.
        await renamer.RunAsync();

        IReadOnlyList<string> pathsAfterFirst = storage.AllPaths();

        // Second run: old-slug no longer exists, so the renamer skips it.
        await renamer.RunAsync();

        IReadOnlyList<string> pathsAfterSecond = storage.AllPaths();

        pathsAfterSecond.Should().BeEquivalentTo(pathsAfterFirst);
    }

    [Fact]
    public async Task Collision_LeavesOldDirectoryUntouched()
    {
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        // Both old and new slugs exist.
        storage.Seed(
            "encodes/old-slug/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("old-slug"))
        );
        storage.Seed("encodes/old-slug/segment.m4s", [0x01]);
        storage.Seed(
            "encodes/new-slug/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("new-slug"))
        );
        storage.Seed("encodes/new-slug/segment.m4s", [0x02]);

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(
            new() { ["old-slug"] = "new-slug" },
            storage,
            context
        );

        Func<Task> act = () => renamer.RunAsync();

        // Must not throw.
        await act.Should().NotThrowAsync();

        // Old directory must still be present and not destroyed.
        storage.AllPaths().Should().Contain("encodes/old-slug/manifest.json");
        storage.AllPaths().Should().Contain("encodes/old-slug/segment.m4s");

        // New directory must be untouched (not overwritten with old content).
        string? newManifestJson = storage.ReadString("encodes/new-slug/manifest.json");
        newManifestJson.Should().NotBeNull();
        JObject? obj = JsonConvert.DeserializeObject<JObject>(newManifestJson!);
        obj!["preset_slug"]!.Value<string>().Should().Be("new-slug");
    }

    [Fact]
    public async Task EmptySlugMap_IsNoOp()
    {
        RenameTestStorage storage = new();
        MediaContext context = RenameTestHelpers.BuildInMemoryContext();

        storage.Seed(
            "encodes/some-slug/manifest.json",
            Encoding.UTF8.GetBytes(RenameTestHelpers.BuildManifestJson("some-slug"))
        );

        BundleSlugRenamer renamer = RenameTestHelpers.BuildRenamer(new(), storage, context);

        await renamer.RunAsync();

        // Nothing should have changed.
        storage.AllPaths().Should().Contain("encodes/some-slug/manifest.json");
    }
}
