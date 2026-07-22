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

using System.Reflection;
using NoMercy.NmSystem.Information;

namespace NoMercy.NmSystem.SystemCalls;

internal static class StartupManagerShared
{
    /// <summary>
    /// Finds the Launcher binary. Checks the server's own directory first,
    /// then the standard download location in AppFiles.
    /// </summary>
    internal static string? ResolveLauncherPath()
    {
        // 1. Installer directory (set by the Launcher when it starts the server)
        string? installDir = Environment.GetEnvironmentVariable(variable: "NOMERCY_INSTALL_DIR");
        if (!string.IsNullOrEmpty(value: installDir))
        {
            string candidate = Path.Combine(path1: installDir, path2: "NoMercyLauncher" + Info.ExecSuffix);
            if (File.Exists(path: candidate))
                return candidate;
        }

        // 2. Same directory as the running server process
        string? processDir = Path.GetDirectoryName(path: Environment.ProcessPath);
        if (!string.IsNullOrEmpty(value: processDir))
        {
            string candidate = Path.Combine(path1: processDir, path2: "NoMercyLauncher" + Info.ExecSuffix);
            if (File.Exists(path: candidate))
                return candidate;
        }

        // 3. Standard download location
        if (File.Exists(path: AppFiles.LauncherExePath))
            return AppFiles.LauncherExePath;

        return null;
    }

    internal static string GetExecutablePath()
    {
        // For single-file published apps, Assembly.Location returns empty string.
        // Use Process.MainModule or Environment.ProcessPath instead.
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(value: processPath))
            return processPath;

        return Assembly.GetExecutingAssembly().Location;
    }
}
