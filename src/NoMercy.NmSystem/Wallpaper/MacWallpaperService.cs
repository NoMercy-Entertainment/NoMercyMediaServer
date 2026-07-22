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

using System.Diagnostics;
using System.Runtime.Versioning;

namespace NoMercy.NmSystem.Wallpaper;

[SupportedOSPlatform(platformName: "macos")]
public class MacWallpaperService : IWallpaperService
{
    public bool IsSupported => true;

    public void Set(string imagePath, WallpaperStyle style, string hexColor)
    {
        ApplyWallpaper(imagePath: imagePath);
    }

    public void SetSilent(string imagePath, WallpaperStyle style, string hexColor)
    {
        Set(imagePath: imagePath, style: style, hexColor: hexColor);
    }

    public void Restore()
    {
        // macOS doesn't expose a simple API to restore the previous wallpaper.
        // The OS manages wallpaper history internally.
    }

    private static void ApplyWallpaper(string imagePath)
    {
        // Try the System Events approach first (works on macOS 14+ Sonoma)
        string script =
            $"tell application \"System Events\" to tell every desktop to set picture to \"{imagePath}\"";

        if (!RunOsascript(script: script))
        {
            // Fallback for older macOS versions
            string fallbackScript =
                $"tell application \"Finder\" to set desktop picture to POSIX file \"{imagePath}\"";
            RunOsascript(script: fallbackScript);
        }
    }

    private static bool RunOsascript(string script)
    {
        try
        {
            using Process process = new();
            process.StartInfo = new()
            {
                FileName = "osascript",
                Arguments = $"-e '{script}'",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            process.WaitForExit(milliseconds: 5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
