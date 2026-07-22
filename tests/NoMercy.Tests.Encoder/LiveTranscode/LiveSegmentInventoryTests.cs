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
using NoMercy.Encoder.LiveTranscode;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LiveSegmentInventoryTests
{
    private static LiveSegmentInventory MakeInventory(IStorage storage) =>
        new(storage: storage, logger: NullLogger<LiveSegmentInventory>.Instance);

    [Fact]
    public void Snapshot_ParsesIndicesFromSegmentFilenames()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            storage.Write(path: storage.CombinePath(parent: dir, child: "seg_00000.ts"), bytes: [1]);
            storage.Write(path: storage.CombinePath(parent: dir, child: "seg_00005.ts"), bytes: [1]);
            storage.Write(path: storage.CombinePath(parent: dir, child: "seg_00012.ts"), bytes: [1]);

            IReadOnlySet<int> indices = MakeInventory(storage: storage).Snapshot(scratchDirectory: dir);

            indices.Should().BeEquivalentTo(expectation: [0, 5, 12]);
        }
        finally
        {
            CleanUp(dir: dir);
        }
    }

    [Fact]
    public void Snapshot_IgnoresNonMatchingFiles()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            storage.Write(path: storage.CombinePath(parent: dir, child: "seg_00000.ts"), bytes: [1]);
            storage.Write(path: storage.CombinePath(parent: dir, child: "index.m3u8"), bytes: [1]);
            storage.Write(path: storage.CombinePath(parent: dir, child: "notasegment.txt"), bytes: [1]);

            IReadOnlySet<int> indices = MakeInventory(storage: storage).Snapshot(scratchDirectory: dir);

            indices.Should().BeEquivalentTo(expectation: [0]);
        }
        finally
        {
            CleanUp(dir: dir);
        }
    }

    [Fact]
    public void Snapshot_MissingDirectory_ReturnsEmptySet()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"live-inventory-missing-{Guid.NewGuid():N}");

        IReadOnlySet<int> indices = MakeInventory(storage: storage).Snapshot(scratchDirectory: dir);

        indices.Should().BeEmpty();
    }

    [Fact]
    public void Purge_RemovesSegmentFiles()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            string seg0 = storage.CombinePath(parent: dir, child: "seg_00000.ts");
            string seg1 = storage.CombinePath(parent: dir, child: "seg_00001.ts");
            storage.Write(path: seg0, bytes: [1]);
            storage.Write(path: seg1, bytes: [1]);

            MakeInventory(storage: storage).Purge(scratchDirectory: dir);

            storage.Exists(path: seg0).Should().BeFalse();
            storage.Exists(path: seg1).Should().BeFalse();
        }
        finally
        {
            CleanUp(dir: dir);
        }
    }

    [Fact]
    public void Purge_ToleratesALockedFile_AndStillRemovesTheRest()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            string locked = storage.CombinePath(parent: dir, child: "seg_00000.ts");
            string removable = storage.CombinePath(parent: dir, child: "seg_00001.ts");
            storage.Write(path: locked, bytes: [1]);
            storage.Write(path: removable, bytes: [1]);

            using FileStream handle = new(
                path: Path.Combine(path1: dir, path2: "seg_00000.ts"),
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read
            );

            Action act = () => MakeInventory(storage: storage).Purge(scratchDirectory: dir);

            act.Should().NotThrow();
            storage.Exists(path: removable).Should().BeFalse();
        }
        finally
        {
            CleanUp(dir: dir);
        }
    }

    private static string MakeScratchDir()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"live-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        if (Directory.Exists(path: dir))
            Directory.Delete(path: dir, recursive: true);
    }
}
