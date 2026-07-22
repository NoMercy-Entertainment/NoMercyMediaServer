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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;
using NoMercy.Storage;

namespace NoMercy.OpticalMedia.Rip;

/// <summary>
/// Rips optical-disc titles to intermediate MKV files on disk. Each title
/// becomes <c>{outputDir}/title_{index}.mkv</c> via a stream-copy FFmpeg
/// command — no re-encode at rip time, that happens in a follow-up
/// <c>VideoEncodeJob</c> once the MKV is on disk.
///
/// Stream copy is the right default here: disc codecs (H.264 / HEVC video,
/// AC3 / DTS / TrueHD audio, PGS / VobSub subtitles) all pass straight
/// through to the MKV container so the rip preserves source quality
/// perfectly. Re-encoding happens later with the user's chosen profile.
/// </summary>
public partial class DiscRipper(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorage storage,
    DriveLockRegistry driveLockRegistry,
    ILogger<DiscRipper> logger
) : IDiscRipper
{
    public async Task<DiscRipResult[]> RipAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string lockKey = string.IsNullOrWhiteSpace(value: request.VolumeUuid)
            ? request.DrivePath
            : request.VolumeUuid;

        if (!driveLockRegistry.TryAcquire(driveKey: lockKey, driveLock: out DriveLock? driveLock))
        {
            throw new DiscDriveBusyException(driveKey: lockKey);
        }

        try
        {
            return await RipInternalAsync(request: request, outputDirectory: outputDirectory, ct: ct);
        }
        finally
        {
            driveLock!.Dispose();
        }
    }

    private async Task<DiscRipResult[]> RipInternalAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    )
    {
        storage.CreateDirectory(path: outputDirectory);

        // CD audio discs use a dedicated per-track FLAC path. Each CD-DA
        // track is a separate libcdio audio stream; the shared video path
        // hardcodes -map 0:v:0 which is invalid for audio-only CD-DA.
        if (ResolveDiscType(request: request) == OpticalDiscType.Cd)
            return await RipCdTracksAsync(request: request, outputDirectory: outputDirectory, ct: ct);

        List<DiscRipResult> results = [];
        foreach (int titleIndex in request.SelectedTitleIndices)
        {
            ct.ThrowIfCancellationRequested();
            DiscRipResult result = await RipOneTitleAsync(request: request, titleIndex: titleIndex, outputDirectory: outputDirectory, ct: ct);
            results.Add(item: result);
            if (!result.Success)
                logger.LogWarning(
                    message: "Rip title {Index} from {Drive} failed: {Error}", args: [titleIndex, request.DrivePath, result.Error]
                );
        }
        return results.ToArray();
    }

    /// <summary>
    /// Rips selected CD-DA tracks to individual FLAC files. Each CD-DA
    /// track is exposed by libcdio as a separate audio stream; track N
    /// (1-based) maps to stream index N-1 via <c>-map 0:a:&lt;N-1&gt;</c>.
    ///
    /// One ffmpeg invocation per track so progress / cancellation is
    /// per-track rather than for the whole disc.
    ///
    /// // hardware-validate: confirm libcdio per-track stream mapping with a
    /// // real CD — libcdio presents each track as a distinct audio stream
    /// // indexed from 0 in the order they appear on the disc, which maps
    /// // cleanly to 0:a:0, 0:a:1, … 0:a:N-1. Validated against the probe
    /// // logic in CdDiscSource.ParseTracks (rawIndex + 1 = track number,
    /// // so stream index = trackIndex - 1).
    /// </summary>
    private async Task<DiscRipResult[]> RipCdTracksAsync(
        RipRequest request,
        string outputDirectory,
        CancellationToken ct
    )
    {
        List<DiscRipResult> results = [];

        foreach (int trackIndex in request.SelectedTitleIndices)
        {
            ct.ThrowIfCancellationRequested();
            DiscRipResult result = await RipOneCdTrackAsync(
                request: request,
                trackIndex: trackIndex,
                outputDirectory: outputDirectory,
                ct: ct
            );
            results.Add(item: result);
            if (!result.Success)
                logger.LogWarning(
                    message: "CD track rip {Index} from {Drive} failed: {Error}", args: [trackIndex, request.DrivePath, result.Error]
                );
        }

        return results.ToArray();
    }

    /// <summary>
    /// Rips one CD-DA track to a FLAC file.
    ///
    /// ffmpeg invocation:
    ///   ffmpeg -y -hide_banner -f libcdio -i &lt;drivePath&gt;
    ///          -map 0:a:&lt;trackIndex-1&gt; -c:a flac
    ///          &lt;outputDir&gt;/NN - &lt;sanitized-title&gt;.flac
    /// </summary>
    internal async Task<DiscRipResult> RipOneCdTrackAsync(
        RipRequest request,
        int trackIndex,
        string outputDirectory,
        CancellationToken ct
    )
    {
        // trackIndex is 1-based (matches DiscTrack.Index); stream 0:a:N-1.
        int streamIndex = trackIndex - 1;

        string trackTitle = ResolveTrackTitle(request: request, trackIndex: trackIndex);
        string fileName = $"{trackIndex:D2} - {SanitizeForPath(input: trackTitle)}.flac";
        string outputPath = Path.Combine(path1: outputDirectory, path2: fileName);

        List<string> args =
        [
            "-y",
            "-hide_banner",
            "-f",
            "libcdio",
            "-i",
            request.DrivePath,
            "-map",
            $"0:a:{streamIndex}",
            "-c:a",
            "flac",
        ];

        await using LocalPathLease outputLease = storage.AcquireLocalPath(path: outputPath);
        args.Add(item: outputLease.Path);

        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfmpegPath,
            arguments: args.ToArray(),
            workingDirectory: outputDirectory,
            cancellationToken: ct
        );
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            string stderrTail =
                string.IsNullOrEmpty(value: result.StdErr) ? "(no stderr)"
                : result.StdErr.Length > 800 ? result.StdErr[^800..]
                : result.StdErr;
            logger.LogInformation(
                message: "ffmpeg CD rip failed exit={Exit} args=[{Args}] stderr_tail={Stderr}", args: [result.ExitCode, string.Join(separator: " ", values: args), stderrTail]
            );

            return new(
                TitleIndex: trackIndex,
                OutputPath: outputPath,
                Success: false,
                Duration: stopwatch.Elapsed,
                OutputSizeBytes: 0,
                Error: $"ffmpeg exited with code {result.ExitCode}"
            );
        }

        long size = storage.SizeOrZero(path: outputPath);
        logger.LogInformation(
            message: "Ripped CD track {Index} (stream 0:a:{Stream}) from {Drive} → {Path} ({Bytes} bytes, {Duration:c})", args: [trackIndex, streamIndex, request.DrivePath, outputPath, size, stopwatch.Elapsed]
        );

        return new(
            TitleIndex: trackIndex,
            OutputPath: outputPath,
            Success: true,
            Duration: stopwatch.Elapsed,
            OutputSizeBytes: size,
            Error: null
        );
    }

    private static string ResolveTrackTitle(RipRequest request, int trackIndex)
    {
        // AudioTracks on the request carry the probed track titles from DiscInfo.
        // For a CD rip the SelectedTitleIndices are track numbers, not playlist indices.
        // If the caller didn't populate a title, fall back to "Track NN".
        return $"Track {trackIndex:D2}";
    }

    private static string SanitizeForPath(string input)
    {
        string trimmed = InvalidFsCharsRegex().Replace(input: input, replacement: " ").Trim();
        return WhitespaceRunRegex().Replace(input: trimmed, replacement: " ");
    }

    [GeneratedRegex(pattern: @"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidFsCharsRegex();

    [GeneratedRegex(pattern: @"\s+")]
    private static partial Regex WhitespaceRunRegex();

    private async Task<DiscRipResult> RipOneTitleAsync(
        RipRequest request,
        int titleIndex,
        string outputDirectory,
        CancellationToken ct
    )
    {
        string outputPath = Path.Combine(path1: outputDirectory, path2: $"title_{titleIndex:D2}.mkv");

        OpticalDiscType discType = ResolveDiscType(request: request);
        string inputUrl = BuildInputUrl(drivePath: request.DrivePath, discType: discType, titleIndex: titleIndex);

        List<string> args = ["-y", "-hide_banner"];

        // Per-disc-type input flags. Bluray uses the bluray: protocol with
        // -playlist; DVD needs -f dvdvideo + -title since libdvdread doesn't
        // accept paths via the protocol layer; CD reads via libcdio.
        switch (discType)
        {
            case OpticalDiscType.BluRay:
                args.Add(item: "-playlist");
                args.Add(item: titleIndex.ToString());
                break;
            case OpticalDiscType.Dvd:
                args.Add(item: "-f");
                args.Add(item: "dvdvideo");
                args.Add(item: "-title");
                args.Add(item: titleIndex.ToString());
                break;
            case OpticalDiscType.Cd:
                args.Add(item: "-f");
                args.Add(item: "libcdio");
                break;
        }

        args.Add(item: "-i");
        args.Add(item: inputUrl);

        // Map only the audio / subtitle streams the user opted into.
        args.Add(item: "-map");
        args.Add(item: "0:v:0");

        foreach (AudioTrackSelection audio in request.AudioTracks.Where(predicate: a => a.Include))
        {
            args.Add(item: "-map");
            args.Add(item: $"0:a:{audio.StreamIndex}");
        }

        foreach (SubtitleSelection sub in request.Subtitles.Where(predicate: s => s.Include))
        {
            args.Add(item: "-map");
            args.Add(item: $"0:s:{sub.StreamIndex}");
        }

        // Stream copy — preserves disc-source quality and finishes in
        // read-speed rather than encode-speed time.
        args.Add(item: "-c");
        args.Add(item: "copy");

        // Lease the output path through IStorage so remote drivers can
        // stage it locally for ffmpeg and clean up on dispose.
        await using LocalPathLease outputLease = storage.AcquireLocalPath(path: outputPath);
        args.Add(item: outputLease.Path);

        // Forward AACS / BD+ database overrides into the child process environment
        // when the caller has configured them.  The host process environment is
        // never mutated — we pass a separate dict to the process runner.
        //
        // Variable names sourced from libaacs src/libaacs/aacs.c (LIBAACS_KEY_DB)
        // and libbdplus src/libbdplus/bdplus.c (LIBBDPLUS_DATABASE).
        // Assumption: names are stable; documented here in case a future upstream
        // release renames them.
        Dictionary<string, string>? envOverrides = BuildBluRayEnvOverrides(drivePath: request.DrivePath);

        Stopwatch stopwatch = Stopwatch.StartNew();
        ProcessResult result = envOverrides is { Count: > 0 }
            ? await processRunner.RunAsync(
                executable: options.FfmpegPath,
                arguments: args.ToArray(),
                extraEnv: envOverrides,
                workingDirectory: outputDirectory,
                cancellationToken: ct
            )
            : await processRunner.RunAsync(
                executable: options.FfmpegPath,
                arguments: args.ToArray(),
                workingDirectory: outputDirectory,
                cancellationToken: ct
            );
        stopwatch.Stop();

        if (!result.IsSuccess)
        {
            // Surface structured AACS / BD+ errors when the rip fails mid-stream.
            if (request.DrivePath.StartsWith(value: "bluray:", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                DiscScanner.ClassifyBluRayStderr(drivePath: request.DrivePath, stderr: result.StdErr);
            }

            string stderrTail =
                string.IsNullOrEmpty(value: result.StdErr) ? "(no stderr)"
                : result.StdErr.Length > 800 ? result.StdErr[^800..]
                : result.StdErr;
            logger.LogInformation(
                message: "ffmpeg rip failed exit={Exit} args=[{Args}] stderr_tail={Stderr}", args: [result.ExitCode, string.Join(separator: " ", values: args), stderrTail]
            );

            return new(
                TitleIndex: titleIndex,
                OutputPath: outputPath,
                Success: false,
                Duration: stopwatch.Elapsed,
                OutputSizeBytes: 0,
                Error: $"ffmpeg exited with code {result.ExitCode}"
            );
        }

        long size = storage.SizeOrZero(path: outputPath);
        logger.LogInformation(
            message: "Ripped title {Index} from {Drive} → {Path} ({Bytes} bytes, {Duration:c})", args: [titleIndex, request.DrivePath, outputPath, size, stopwatch.Elapsed]
        );

        return new(
            TitleIndex: titleIndex,
            OutputPath: outputPath,
            Success: true,
            Duration: stopwatch.Elapsed,
            OutputSizeBytes: size,
            Error: null
        );
    }

    /// <summary>
    /// Resolves the disc type the rip should use. Prefers the
    /// <see cref="RipRequest.DiscType"/> the caller populated; falls back
    /// to sniffing the drive-path prefix for raw API calls that don't
    /// supply it.
    /// </summary>
    private static OpticalDiscType ResolveDiscType(RipRequest request)
    {
        if (request.DiscType != OpticalDiscType.None)
            return request.DiscType;
        if (request.DrivePath.StartsWith(value: "bluray:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return OpticalDiscType.BluRay;
        if (request.DrivePath.StartsWith(value: "dvd:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return OpticalDiscType.Dvd;
        return OpticalDiscType.None;
    }

    /// <summary>
    /// Builds the ffmpeg <c>-i</c> URL for the disc + title. Bluray expects
    /// <c>bluray:&lt;mount&gt;/</c> with a trailing separator; DVD points at
    /// the VIDEO_TS folder so libdvdread's filesystem fallback works on
    /// virtual / USB drives that don't expose SCSI MMC; CD passes the raw
    /// device path to libcdio.
    /// </summary>
    private static string BuildInputUrl(string drivePath, OpticalDiscType discType, int titleIndex)
    {
        if (drivePath.StartsWith(value: "bluray:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return drivePath;
        if (drivePath.StartsWith(value: "dvd:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return drivePath;

        string trimmed = drivePath.TrimEnd(trimChars: ['\\', '/']);
        return discType switch
        {
            OpticalDiscType.BluRay => $"bluray:{trimmed}/",
            OpticalDiscType.Dvd => $"{trimmed}/VIDEO_TS/",
            OpticalDiscType.Cd => drivePath,
            _ => drivePath,
        };
    }

    /// <summary>
    /// Builds the environment-variable overrides to pass to the ffmpeg child
    /// process for Blu-ray disc paths. Returns null / empty when no overrides
    /// are configured (DVD paths also return null — they don't use libaacs).
    /// </summary>
    private Dictionary<string, string>? BuildBluRayEnvOverrides(string drivePath)
    {
        if (!drivePath.StartsWith(value: "bluray:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return null;

        BluRayOptions? bluRay = options.BluRay;
        if (bluRay is null)
            return null;

        Dictionary<string, string> env = [];

        if (!string.IsNullOrWhiteSpace(value: bluRay.KeyDbOverridePath))
            env[key: "LIBAACS_KEY_DB"] = bluRay.KeyDbOverridePath;

        if (!string.IsNullOrWhiteSpace(value: bluRay.AacsKeysOverridePath))
            env[key: "LIBBDPLUS_DATABASE"] = bluRay.AacsKeysOverridePath;

        return env.Count > 0 ? env : null;
    }
}
