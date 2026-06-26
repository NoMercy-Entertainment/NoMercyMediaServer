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
using Newtonsoft.Json;
using NoMercy.Encoder.Bundle;
using NoMercy.Storage;

namespace NoMercy.Tests.Encoder.Bundle;

// ---------------------------------------------------------------------------
// In-memory IStorage test double
// ---------------------------------------------------------------------------

internal sealed class TestStorage : IStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(
        StringComparer.OrdinalIgnoreCase
    );

    // Test-side helper: read stored bytes back as a UTF-8 string.
    public string? ReadString(string path) =>
        _files.TryGetValue(path, out byte[]? bytes) ? Encoding.UTF8.GetString(bytes) : null;

    // Seed a file so Reconcile tests can simulate on-disk state.
    public void Seed(string path, byte[] bytes) => _files[path] = bytes;

    // -----------------------------------------------------------------------
    // IStorage — only the members BundleManifestWriter actually calls
    // -----------------------------------------------------------------------

    public Task WriteAsync(string path, byte[] bytes, CancellationToken ct)
    {
        _files[path] = bytes;
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadAsync(string path, CancellationToken ct)
    {
        if (!_files.TryGetValue(path, out byte[]? bytes))
            throw new FileNotFoundException($"TestStorage: path not found: {path}");
        return Task.FromResult(bytes);
    }

    public bool Exists(string path) => _files.ContainsKey(path);

    public IReadOnlyList<StorageEntry> List(string path, string? pattern, bool recursive)
    {
        string prefix = path.TrimEnd('/') + "/";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<StorageEntry> entries = [];
        foreach (string key in _files.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string rel = key[prefix.Length..];
            if (!recursive && rel.Contains('/'))
                continue;
            entries.Add(
                new StorageEntry(
                    key,
                    IsDirectory: false,
                    SizeBytes: _files[key].Length,
                    LastModified: now
                )
            );
        }
        return entries;
    }

    // -----------------------------------------------------------------------
    // Remaining IStorage members — not exercised by BundleManifestWriter
    // -----------------------------------------------------------------------

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<Stream> OpenWriteAsync(string path, bool overwrite, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<bool> ExistsAsync(string path, CancellationToken ct) =>
        Task.FromResult(Exists(path));

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        _files.TryRemove(path, out _);
        return Task.CompletedTask;
    }

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

    public long SizeOrZero(string path) => _files.TryGetValue(path, out byte[]? b) ? b.Length : 0;

    public long Size(string path) =>
        _files.TryGetValue(path, out byte[]? b) ? b.Length : throw new FileNotFoundException(path);

    public DateTimeOffset LastModified(string path) => DateTimeOffset.UtcNow;

    public void CreateDirectory(string path) { }

    public void Delete(string path) => _files.TryRemove(path, out _);

    public void DeleteDirectory(string path, bool recursive) => throw new NotSupportedException();

    public byte[] Read(string path) =>
        _files.TryGetValue(path, out byte[]? bytes) ? bytes : throw new FileNotFoundException(path);

    public Stream OpenRead(string path) => throw new NotSupportedException();

    public Stream OpenWrite(string path, bool overwrite) => throw new NotSupportedException();

    public void Write(string path, byte[] bytes) => _files[path] = bytes;

    public void Move(string from, string to) => throw new NotSupportedException();

    public void Copy(string from, string to) => throw new NotSupportedException();

    public LocalPathLease AcquireLocalPath(string path) => throw new NotSupportedException();

    public Task<string> ReadAllTextAsync(string path, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task MoveDirectoryAsync(string from, string to, CancellationToken ct) =>
        throw new NotSupportedException();

    public void MoveDirectory(string from, string to) => throw new NotSupportedException();

    public IStorageDriver Driver => throw new NotSupportedException();
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

public class BundleManifestWriterTests
{
    private static BundleManifest SampleManifest() =>
        new(
            Version: 1,
            EncoderVersion: "3.0.0",
            PresetId: "01HZABC",
            PresetName: "Web 1080p",
            PresetSlug: "web-1080p",
            MediaType: "movie",
            MediaId: 550,
            MediaExternalId: null,
            MediaFolder: "/media/movies/Fight Club (1999)",
            Container: "hls-fmp4",
            CreatedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CompletedAt: new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            MediaKey: "mfa",
            Files:
            [
                "mfa_master.m3u8",
                "video/1080p/mfa_1080p_init.mp4",
                "video/1080p/mfa_1080p_00001.m4s",
            ]
        );

    [Fact]
    public async Task WriteAsync_PersistsManifestJson()
    {
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);
        BundleManifest manifest = SampleManifest();

        await writer.WriteAsync(
            "encodes/web-1080p/manifest.json",
            manifest,
            CancellationToken.None
        );

        string? json = storage.ReadString("encodes/web-1080p/manifest.json");
        json.Should().NotBeNull();
        BundleManifest? roundtrip = JsonConvert.DeserializeObject<BundleManifest>(json!);
        roundtrip.Should().BeEquivalentTo(manifest);
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullWhenMissing()
    {
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);

        BundleManifest? result = await writer.ReadAsync(
            "encodes/web-1080p/manifest.json",
            CancellationToken.None
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task ReconcileAsync_ReportsExtraFilesAsWarning()
    {
        // Manifest claims 3 files; disk has those 3 + 1 extra.
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);
        BundleManifest manifest = SampleManifest(); // files: master + 2 segments

        // Seed the 3 manifest files + 1 extra.
        string bundleDir = "encodes/web-1080p";
        foreach (string f in manifest.Files)
            storage.Seed($"{bundleDir}/{f}", [0x01]);
        storage.Seed($"{bundleDir}/extra_orphan.ts", [0x02]);

        ReconcileReport report = await writer.ReconcileAsync(
            bundleDir,
            manifest,
            CancellationToken.None
        );

        report.ExtraFiles.Should().ContainSingle().Which.Should().Be("extra_orphan.ts");
        report.MissingFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconcileAsync_ReportsMissingFilesAsWarning()
    {
        // Manifest claims 3 files; disk only has 2.
        TestStorage storage = new();
        BundleManifestWriter writer = new(storage);
        BundleManifest manifest = SampleManifest(); // files: master + 2 segments

        string bundleDir = "encodes/web-1080p";
        // Seed only 2 of the 3 manifest files.
        storage.Seed($"{bundleDir}/{manifest.Files[0]}", [0x01]);
        storage.Seed($"{bundleDir}/{manifest.Files[1]}", [0x01]);
        // manifest.Files[2] is intentionally absent.

        ReconcileReport report = await writer.ReconcileAsync(
            bundleDir,
            manifest,
            CancellationToken.None
        );

        report.MissingFiles.Should().ContainSingle().Which.Should().Be(manifest.Files[2]);
        report.ExtraFiles.Should().BeEmpty();
    }
}
