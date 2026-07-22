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

using NoMercy.MediaProcessing.Files;
using Xunit;

namespace NoMercy.Tests.MediaProcessing.Files;

/// <summary>
/// After a FileSystemWatcher raises an error (e.g. InternalBufferOverflow) it stops
/// raising events; the watched folder was going permanently blind because the error
/// handler never re-armed it. TryReArm restores event delivery.
/// </summary>
public class FolderWatcherReArmTests
{
    [Fact]
    public void TryReArm_ReEnablesAStoppedWatcher()
    {
        string dir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nm-fw-rearm-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: dir);
        try
        {
            using FileSystemWatcher watcher = new(path: dir) { EnableRaisingEvents = true };
            watcher.EnableRaisingEvents = false;

            bool result = FolderWatcher.TryReArm(watcher: watcher);

            Assert.True(condition: result);
            Assert.True(condition: watcher.EnableRaisingEvents);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void TryReArm_NullWatcher_ReturnsFalse()
    {
        Assert.False(condition: FolderWatcher.TryReArm(watcher: null));
    }
}
