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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveTranscodeOrphanSweeperTests : IDisposable
{
    private readonly string _cacheRoot = Path.Combine(
        path1: Path.GetTempPath(),
        path2: "nomercy-orphan-test-" + Ulid.NewUlid()
    );

    public void Dispose()
    {
        if (Directory.Exists(path: _cacheRoot))
            Directory.Delete(path: _cacheRoot, recursive: true);
    }

    private static IStorage MakeStorage() =>
        new LocalStorage(
            driver: new LocalStorageDriver(),
            guard: new(allowedRoots: [], driver: new LocalStorageDriver())
        );

    private LiveTranscodeOrphanSweeper BuildSweeper()
    {
        EncoderOptions opts = new() { LiveTranscodeCachePath = _cacheRoot };
        return new(
            options: opts,
            logger: NullLogger<LiveTranscodeOrphanSweeper>.Instance,
            storage: MakeStorage()
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // lts-* directories are deleted on startup
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_DeletesLtsPrefixedDirectories()
    {
        Directory.CreateDirectory(path: _cacheRoot);
        string orphan1 = Path.Combine(path1: _cacheRoot, path2: "lts-01J0000000000000000000000A");
        string orphan2 = Path.Combine(path1: _cacheRoot, path2: "lts-01J0000000000000000000000B");
        Directory.CreateDirectory(path: orphan1);
        Directory.CreateDirectory(path: orphan2);

        LiveTranscodeOrphanSweeper sweeper = BuildSweeper();
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: orphan1).Should().BeFalse();
        Directory.Exists(path: orphan2).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Non-lts directories are preserved
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_PreservesNonLtsDirectories()
    {
        Directory.CreateDirectory(path: _cacheRoot);
        string keepDir = Path.Combine(path1: _cacheRoot, path2: "thumbnails");
        Directory.CreateDirectory(path: keepDir);

        LiveTranscodeOrphanSweeper sweeper = BuildSweeper();
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: keepDir).Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Files inside lts-* directory are also removed (recursive delete)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_RecursivelyDeletesOrphanContents()
    {
        Directory.CreateDirectory(path: _cacheRoot);
        string orphanDir = Path.Combine(path1: _cacheRoot, path2: "lts-01J0000000000000000000000C");
        Directory.CreateDirectory(path: orphanDir);
        string segFile = Path.Combine(path1: orphanDir, path2: "seg_00001.ts");
        await File.WriteAllBytesAsync(path: segFile, bytes: [0x00, 0x01, 0x02]);

        LiveTranscodeOrphanSweeper sweeper = BuildSweeper();
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: orphanDir).Should().BeFalse();
        File.Exists(path: segFile).Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Missing cache root — no throw
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_MissingCacheRoot_DoesNotThrow()
    {
        // Use a path that definitely doesn't exist.
        EncoderOptions opts = new()
        {
            LiveTranscodeCachePath = Path.Combine(
                path1: Path.GetTempPath(),
                path2: "nomercy-does-not-exist-" + Ulid.NewUlid()
            ),
        };

        LiveTranscodeOrphanSweeper sweeper = new(
            options: opts,
            logger: NullLogger<LiveTranscodeOrphanSweeper>.Instance,
            storage: MakeStorage()
        );

        Func<Task> act = () => sweeper.StartAsync(cancellationToken: CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Empty cache root — no throw
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_EmptyCacheRoot_DoesNotThrow()
    {
        Directory.CreateDirectory(path: _cacheRoot);

        LiveTranscodeOrphanSweeper sweeper = BuildSweeper();

        Func<Task> act = () => sweeper.StartAsync(cancellationToken: CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Bug 1 — sweeper glob matches the lts-{ulid} format LiveEncoder creates
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_DeletesDirectoryNamedLtsFollowedByUlid()
    {
        Directory.CreateDirectory(path: _cacheRoot);
        string sessionId = Ulid.NewUlid().ToString();
        string sessionDir = Path.Combine(path1: _cacheRoot, path2: $"lts-{sessionId}");
        Directory.CreateDirectory(path: sessionDir);

        LiveTranscodeOrphanSweeper sweeper = BuildSweeper();
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: sessionDir).Should().BeFalse();
    }
}
