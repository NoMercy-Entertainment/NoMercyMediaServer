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
        Path.GetTempPath(),
        "nomercy-transcoderoot-test-" + Ulid.NewUlid()
    );

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static IStorage MakeStorage() =>
        new LocalStorage(new LocalStorageDriver(), new([], new LocalStorageDriver()));

    private TranscodeRootOrphanSweeper BuildSweeper(bool sweepAllChildren) =>
        new(
            NullLogger<TranscodeRootOrphanSweeper>.Instance,
            MakeStorage(),
            _root,
            sweepAllChildren
        );

    [Fact]
    public async Task StartAsync_FallbackMode_DeletesOnlyEncPrefixedDirs()
    {
        Directory.CreateDirectory(_root);
        string enc1 = Path.Combine(_root, "nomercy-enc-01J0000000000000000000000A");
        string enc2 = Path.Combine(_root, "nomercy-enc-01J0000000000000000000000B");
        string unrelatedDir = Path.Combine(_root, "some-other-app-cache");
        string unrelatedFile = Path.Combine(_root, "unrelated.tmp");
        Directory.CreateDirectory(enc1);
        Directory.CreateDirectory(enc2);
        Directory.CreateDirectory(unrelatedDir);
        await File.WriteAllTextAsync(unrelatedFile, "keep me");

        TranscodeRootOrphanSweeper sweeper = BuildSweeper(false);
        await sweeper.StartAsync(CancellationToken.None);

        Directory.Exists(enc1).Should().BeFalse();
        Directory.Exists(enc2).Should().BeFalse();
        Directory.Exists(unrelatedDir).Should().BeTrue("fallback mode must not touch foreign dirs");
        File.Exists(unrelatedFile).Should().BeTrue("fallback mode must not touch foreign files");
    }

    [Fact]
    public async Task StartAsync_DedicatedMode_DeletesAllChildDirsIncludingMirroredShowDirs()
    {
        Directory.CreateDirectory(_root);
        string enc = Path.Combine(_root, "nomercy-enc-01J0000000000000000000000A");
        string mirroredShow = Path.Combine(_root, "The Show.(2021)");
        string strayFile = Path.Combine(_root, "leftover.log");
        Directory.CreateDirectory(enc);
        Directory.CreateDirectory(mirroredShow);
        await File.WriteAllTextAsync(strayFile, "log");

        TranscodeRootOrphanSweeper sweeper = BuildSweeper(true);
        await sweeper.StartAsync(CancellationToken.None);

        Directory.Exists(enc).Should().BeFalse();
        Directory
            .Exists(mirroredShow)
            .Should()
            .BeFalse("dedicated cache sweep must reclaim mirrored show dirs too");
        File.Exists(strayFile).Should().BeTrue("only directories are swept");
    }

    [Fact]
    public async Task StartAsync_MissingRoot_DoesNotThrow()
    {
        TranscodeRootOrphanSweeper sweeper = BuildSweeper(true);

        Func<Task> act = () => sweeper.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
