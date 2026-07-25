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
        new(storage, NullLogger<LiveSegmentInventory>.Instance);

    [Fact]
    public void Snapshot_ParsesIndicesFromSegmentFilenames()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            storage.Write(storage.CombinePath(dir, "seg_00000.ts"), [1]);
            storage.Write(storage.CombinePath(dir, "seg_00005.ts"), [1]);
            storage.Write(storage.CombinePath(dir, "seg_00012.ts"), [1]);

            IReadOnlySet<int> indices = MakeInventory(storage).Snapshot(dir);

            indices.Should().BeEquivalentTo([0, 5, 12]);
        }
        finally
        {
            CleanUp(dir);
        }
    }

    [Fact]
    public void Snapshot_IgnoresNonMatchingFiles()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            storage.Write(storage.CombinePath(dir, "seg_00000.ts"), [1]);
            storage.Write(storage.CombinePath(dir, "index.m3u8"), [1]);
            storage.Write(storage.CombinePath(dir, "notasegment.txt"), [1]);

            IReadOnlySet<int> indices = MakeInventory(storage).Snapshot(dir);

            indices.Should().BeEquivalentTo([0]);
        }
        finally
        {
            CleanUp(dir);
        }
    }

    [Fact]
    public void Snapshot_MissingDirectory_ReturnsEmptySet()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = Path.Combine(Path.GetTempPath(), $"live-inventory-missing-{Guid.NewGuid():N}");

        IReadOnlySet<int> indices = MakeInventory(storage).Snapshot(dir);

        indices.Should().BeEmpty();
    }

    [Fact]
    public void Purge_RemovesSegmentFiles()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            string seg0 = storage.CombinePath(dir, "seg_00000.ts");
            string seg1 = storage.CombinePath(dir, "seg_00001.ts");
            storage.Write(seg0, [1]);
            storage.Write(seg1, [1]);

            MakeInventory(storage).Purge(dir);

            storage.Exists(seg0).Should().BeFalse();
            storage.Exists(seg1).Should().BeFalse();
        }
        finally
        {
            CleanUp(dir);
        }
    }

    [Fact]
    public void Purge_ToleratesALockedFile_AndStillRemovesTheRest()
    {
        IStorage storage = TestStorageFactory.CreateLocal();
        string dir = MakeScratchDir();
        try
        {
            string locked = storage.CombinePath(dir, "seg_00000.ts");
            string removable = storage.CombinePath(dir, "seg_00001.ts");
            storage.Write(locked, [1]);
            storage.Write(removable, [1]);

            using FileStream handle = new(
                Path.Combine(dir, "seg_00000.ts"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            Action act = () => MakeInventory(storage).Purge(dir);

            act.Should().NotThrow();
            storage.Exists(removable).Should().BeFalse();
        }
        finally
        {
            CleanUp(dir);
        }
    }

    private static string MakeScratchDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"live-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanUp(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }
}
