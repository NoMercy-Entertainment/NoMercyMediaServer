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

using System.ComponentModel;
using System.Diagnostics;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem.FileSystem;

public static class Locking
{
    public static bool IsFileLocked(string filePath)
    {
        try
        {
            using FileStream stream = File.Open(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            );
            stream.Close();
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only file (or one without write ACLs for this process)
            // throws UnauthorizedAccessException rather than IOException when
            // opened with FileAccess.ReadWrite — treat it the same as locked.
            return true;
        }

        return false;
    }

    public static void CloseApplicationLockingFile(string filePath)
    {
        Logger.Setup($"Closing application locking {filePath}", LogEventLevel.Verbose);

        foreach (Process process in Process.GetProcesses())
            try
            {
                if (process.MainModule?.FileName == null)
                    continue;
                if (
                    !process.MainModule.FileName.Equals(
                        filePath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    continue;

                // Never kill the process holding the file — it may be an
                // in-flight ffmpeg encode (or ffprobe/ffplay), and killing it
                // destroys the user's running job. Log and let the caller's
                // own retry/defer logic handle the file staying locked.
                Logger.System(
                    $"{process.ProcessName} (pid {process.Id}) holds {filePath} — "
                        + "skipping cleanup to avoid killing an in-flight process.",
                    LogEventLevel.Warning
                );

                break;
            }
            catch (Win32Exception)
            {
                // Ignore the error if the process is not accessible
            }
            catch (InvalidOperationException ex)
            {
                Logger.System(
                    $"Process {process.ProcessName} has already exited: {ex.Message}",
                    LogEventLevel.Warning
                );
            }
    }
}
