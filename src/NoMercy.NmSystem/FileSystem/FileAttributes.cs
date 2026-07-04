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

using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public static class FileAttributes
{
    public static Task SetCreatedAttribute(string filePath, DateTimeOffset createdAt)
    {
        try
        {
            File.SetCreationTimeUtc(filePath, createdAt.UtcDateTime);
            // CheckLocalVersion compares the file's last-write time against the
            // release date. Archive-extracted binaries (ffmpeg/ffprobe/ffplay)
            // keep the zip entry's original mtime, which predates the release,
            // so without this they look stale and re-download on every boot.
            File.SetLastWriteTimeUtc(filePath, createdAt.UtcDateTime);

            Logger.System(
                $"Set creation and modification dates for {filePath} to {createdAt}",
                LogEventLevel.Verbose
            );
        }
        catch (Exception ex)
        {
            Logger.System(
                $"Failed to set file attributes for {filePath}: {ex.Message}",
                LogEventLevel.Warning
            );
        }

        return Task.CompletedTask;
    }
}
