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
using NoMercy.Encoder.Orchestration;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;

namespace NoMercy.Tests.Encoder.Orchestration;

public class TranscodeRootOrphanSweeperTests : IDisposable
{
    private readonly string _root = Path.Combine(
        path1: Path.GetTempPath(),
        path2: "nomercy-transcoderoot-test-" + Ulid.NewUlid()
    );

    public void Dispose()
    {
        if (Directory.Exists(path: _root))
            Directory.Delete(path: _root, recursive: true);
    }

    private static IStorage MakeStorage() =>
        new LocalStorage(driver: new LocalStorageDriver(), guard: new(allowedRoots: [], driver: new LocalStorageDriver()));

    private TranscodeRootOrphanSweeper BuildSweeper(bool sweepAllChildren) =>
        new(
            logger: NullLogger<TranscodeRootOrphanSweeper>.Instance,
            storage: MakeStorage(),
            root: _root,
            sweepAllChildren: sweepAllChildren
        );

    [Fact]
    public async Task StartAsync_FallbackMode_DeletesOnlyEncPrefixedDirs()
    {
        Directory.CreateDirectory(path: _root);
        string enc1 = Path.Combine(path1: _root, path2: "nomercy-enc-01J0000000000000000000000A");
        string enc2 = Path.Combine(path1: _root, path2: "nomercy-enc-01J0000000000000000000000B");
        string unrelatedDir = Path.Combine(path1: _root, path2: "some-other-app-cache");
        string unrelatedFile = Path.Combine(path1: _root, path2: "unrelated.tmp");
        Directory.CreateDirectory(path: enc1);
        Directory.CreateDirectory(path: enc2);
        Directory.CreateDirectory(path: unrelatedDir);
        await File.WriteAllTextAsync(path: unrelatedFile, contents: "keep me");

        TranscodeRootOrphanSweeper sweeper = BuildSweeper(sweepAllChildren: false);
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: enc1).Should().BeFalse();
        Directory.Exists(path: enc2).Should().BeFalse();
        Directory.Exists(path: unrelatedDir).Should().BeTrue(because: "fallback mode must not touch foreign dirs");
        File.Exists(path: unrelatedFile).Should().BeTrue(because: "fallback mode must not touch foreign files");
    }

    [Fact]
    public async Task StartAsync_DedicatedMode_DeletesAllChildDirsIncludingMirroredShowDirs()
    {
        Directory.CreateDirectory(path: _root);
        string enc = Path.Combine(path1: _root, path2: "nomercy-enc-01J0000000000000000000000A");
        string mirroredShow = Path.Combine(path1: _root, path2: "The Show.(2021)");
        string strayFile = Path.Combine(path1: _root, path2: "leftover.log");
        Directory.CreateDirectory(path: enc);
        Directory.CreateDirectory(path: mirroredShow);
        await File.WriteAllTextAsync(path: strayFile, contents: "log");

        TranscodeRootOrphanSweeper sweeper = BuildSweeper(sweepAllChildren: true);
        await sweeper.StartAsync(cancellationToken: CancellationToken.None);

        Directory.Exists(path: enc).Should().BeFalse();
        Directory
            .Exists(path: mirroredShow)
            .Should()
            .BeFalse(because: "dedicated cache sweep must reclaim mirrored show dirs too");
        File.Exists(path: strayFile).Should().BeTrue(because: "only directories are swept");
    }

    [Fact]
    public async Task StartAsync_MissingRoot_DoesNotThrow()
    {
        TranscodeRootOrphanSweeper sweeper = BuildSweeper(sweepAllChildren: true);

        Func<Task> act = () => sweeper.StartAsync(cancellationToken: CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
