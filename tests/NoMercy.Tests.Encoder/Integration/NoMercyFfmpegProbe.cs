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

namespace NoMercy.Tests.Encoder.Integration;

/// <summary>
/// Resolves the path to the nomercy-ffmpeg fork for integration tests.
///
/// Real-encode integration tests exercise custom muxers (e.g. <c>spritevtt</c>
/// with its <c>-vtt_filename</c> private option) that exist only in the
/// nomercy-ffmpeg fork. Pointing at the system PATH "ffmpeg" — which on dev
/// machines is usually a stock build like gyan.dev — produces
/// <c>Unrecognized option 'vtt_filename'</c> from ffmpeg and the test fails
/// for an environment reason, not a code reason.
///
/// Resolution order:
///   1. <c>NOMERCY_FFMPEG_PATH</c> env var (CI overrides this).
///   2. Standard dev install: <c>%LOCALAPPDATA%/NoMercy_dev/binaries/ffmpeg</c>
///      or <c>~/.local/share/NoMercy_dev/binaries/ffmpeg</c>.
///   3. Standard prod install: <c>%LOCALAPPDATA%/NoMercy/binaries/ffmpeg</c>
///      or <c>~/.local/share/NoMercy/binaries/ffmpeg</c>.
/// Returns null when no fork binary is found — caller skips the test rather
/// than running it against stock ffmpeg and reporting a false failure.
/// </summary>
internal static class NoMercyFfmpegProbe
{
    private const string SpritevttMarker = "spritevtt";

    public static string? ResolveFfmpegPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(variable: "NOMERCY_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(value: overridePath) && File.Exists(path: overridePath))
            return overridePath;

        string binaryName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

        foreach (string root in CandidateRoots())
        {
            string candidate = Path.Combine(path1: root, path2: "binaries", path3: "ffmpeg", path4: binaryName);
            if (File.Exists(path: candidate))
                return candidate;
        }

        return null;
    }

    public static string? ResolveFfprobePath(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(value: ffmpegPath))
            return null;

        string dir = Path.GetDirectoryName(path: ffmpegPath) ?? string.Empty;
        string probeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        string candidate = Path.Combine(path1: dir, path2: probeName);
        return File.Exists(path: candidate) ? candidate : null;
    }

    /// <summary>
    /// Resolves shaka-packager, which the binaries download step installs next to
    /// ffmpeg (<c>binaries/ffmpeg/packager[.exe]</c>). Honours
    /// <c>SHAKA_PACKAGER_PATH</c> first, then the same candidate roots as ffmpeg.
    /// </summary>
    public static string? ResolveShakaPackagerPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(variable: "SHAKA_PACKAGER_PATH");
        if (!string.IsNullOrWhiteSpace(value: overridePath) && File.Exists(path: overridePath))
            return overridePath;

        string binaryName = OperatingSystem.IsWindows() ? "packager.exe" : "packager";

        foreach (string root in CandidateRoots())
        {
            string candidate = Path.Combine(path1: root, path2: "binaries", path3: "ffmpeg", path4: binaryName);
            if (File.Exists(path: candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Confirms the resolved binary is actually the nomercy-ffmpeg fork by
    /// asking it about the spritevtt muxer. Stock ffmpeg reports
    /// "Unknown muxer" on stderr; the fork prints the muxer's options block
    /// to stdout. Distinguishes a renamed binary from a true fork.
    /// </summary>
    public static bool SupportsSpritevtt(string ffmpegPath)
    {
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(item: "-hide_banner");
            psi.ArgumentList.Add(item: "-h");
            psi.ArgumentList.Add(item: "muxer=spritevtt");

            using Process process = Process.Start(startInfo: psi)!;
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(milliseconds: 5000);

            string combined = stdout + stderr;
            return combined.Contains(value: SpritevttMarker, comparisonType: StringComparison.OrdinalIgnoreCase)
                && !combined.Contains(value: "Unknown muxer", comparisonType: StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> CandidateRoots()
    {
        string? localAppData = Environment.GetFolderPath(
            folder: Environment.SpecialFolder.LocalApplicationData
        );
        string? home = Environment.GetEnvironmentVariable(variable: "HOME");

        if (!string.IsNullOrWhiteSpace(value: localAppData))
        {
            yield return Path.Combine(path1: localAppData, path2: "NoMercy_dev");
            yield return Path.Combine(path1: localAppData, path2: "NoMercy");
        }

        if (!string.IsNullOrWhiteSpace(value: home))
        {
            yield return Path.Combine(path1: home, path2: ".local", path3: "share", path4: "NoMercy_dev");
            yield return Path.Combine(path1: home, path2: ".local", path3: "share", path4: "NoMercy");
        }
    }
}
