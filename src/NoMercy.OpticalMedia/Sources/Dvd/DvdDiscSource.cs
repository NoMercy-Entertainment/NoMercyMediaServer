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

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Sources.Dvd;

/// <summary>
/// DVD-Video disc reader. Uses nomercy-ffmpeg's <c>dvdvideo</c> demuxer
/// (libdvdread + libdvdnav with <c>-ldvdcss</c> for CSS decryption).
/// <see cref="ProbeAsync"/> enumerates every title libdvdread sees and
/// returns a skeleton <see cref="DiscInfo"/>. <see cref="ProbeTitleAsync"/>
/// runs a per-title JSON probe for streams + chapters.
///
/// Unlike Bluray's AACS, DVD CSS is reliably decrypted on every drive
/// libdvdcss supports, so unprotected playback / rip is the default path.
/// </summary>
public sealed class DvdDiscSource(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<DvdDiscSource> logger
) : IDiscSource
{
    public OpticalDiscType Type => OpticalDiscType.Dvd;

    public async Task<DiscInfo> ProbeAsync(DiscDrive drive, CancellationToken ct)
    {
        string drivePath = ToDvdPath(drive.Path);

        // libdvdread doesn't enumerate titles in a single call — it expects
        // the caller to walk titles 1..N sequentially. We probe each title
        // with -show_format until ffprobe emits "Title N not found" on
        // stderr, which is the enumeration boundary.
        List<DiscTitle> titles = new();
        DiscProtection? protection = null;
        for (int titleIdx = 1; titleIdx <= MaxTitleProbes; titleIdx++)
        {
            ProcessResult result = await processRunner.RunAsync(
                options.FfprobePath,
                [
                    "-hide_banner",
                    "-v",
                    "error",
                    "-f",
                    "dvdvideo",
                    "-title",
                    titleIdx.ToString(CultureInfo.InvariantCulture),
                    "-print_format",
                    "json",
                    "-show_format",
                    "-i",
                    drivePath,
                ],
                null,
                ct
            );

            // Capture protection state from the first probe — same stderr
            // signatures every iteration anyway.
            protection ??= ClassifyProtection(result.StdErr);

            if (
                !string.IsNullOrEmpty(result.StdErr)
                && result.StdErr.Contains(
                    $"Title {titleIdx} not found",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                // Boundary hit — stop walking.
                break;
            }

            if (string.IsNullOrWhiteSpace(result.StdOut))
                continue;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(result.StdOut);
                if (!doc.RootElement.TryGetProperty("format", out JsonElement format))
                    continue;

                TimeSpan duration = TimeSpan.Zero;
                if (
                    format.TryGetProperty("duration", out JsonElement dur)
                    && double.TryParse(
                        dur.GetString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double seconds
                    )
                )
                {
                    duration = TimeSpan.FromSeconds(seconds);
                }

                titles.Add(
                    new(
                        titleIdx,
                        $"Title {titleIdx:D2}",
                        duration,
                        [],
                        [],
                        [],
                        [],
                        0,
                        false
                    )
                );
            }
            catch (JsonException ex)
            {
                logger.LogInformation(
                    ex,
                    "DVD title {Title} probe parse failed for {Drive}", [titleIdx, drive.Path]
                );
            }
        }

        if (titles.Count == 0)
        {
            logger.LogInformation(
                "DVD probe found 0 titles for {Drive}: {Protection}", [drive.Path, protection?.Message ?? "(no protection detected)"]
            );
            return new(OpticalDiscType.Dvd, drive.Label, [], null, TimeSpan.Zero, protection);
        }

        // Flag the longest title as the main feature.
        TimeSpan maxDuration = titles.Max(t => t.Duration);
        DiscTitle[] flagged = titles
            .Select(t => t with { IsMainFeature = t.Duration == maxDuration })
            .OrderByDescending(t => t.Duration)
            .ToArray();

        return new(
            OpticalDiscType.Dvd,
            drive.Label,
            flagged,
            null,
            flagged.Sum(t => t.Duration.Ticks) is long ticks
                ? TimeSpan.FromTicks(ticks)
                : TimeSpan.Zero,
            protection
        );
    }

    /// <summary>
    /// DVD-Video has at most 99 titles; in practice retail discs rarely
    /// exceed the low double digits. We cap the walk to keep cold probes
    /// fast — single-title discs walk one ffprobe; full retail discs
    /// walk ~30 in the worst case.
    /// </summary>
    private const int MaxTitleProbes = 50;

    public async Task<DiscTitle> ProbeTitleAsync(
        DiscDrive drive,
        int titleIndex,
        CancellationToken ct
    )
    {
        string drivePath = ToDvdPath(drive.Path);

        ProcessResult result = await processRunner.RunAsync(
            options.FfprobePath,
            [
                "-v",
                "quiet",
                "-f",
                "dvdvideo",
                "-title",
                titleIndex.ToString(CultureInfo.InvariantCulture),
                "-print_format",
                "json",
                "-show_format",
                "-show_streams",
                "-show_chapters",
                "-i",
                drivePath,
            ],
            null,
            ct
        );

        DiscTitle empty = new(
            titleIndex,
            $"Title {titleIndex:D2}",
            TimeSpan.Zero,
            [],
            [],
            [],
            [],
            0,
            false
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
            return empty;

        try
        {
            // Re-use Bluray's parser — same JSON shape from ffprobe regardless
            // of input demuxer. Returns DiscInfo with one title; we lift that
            // title out and re-stamp the index.
            DiscInfo info = Bluray.DiscScanner.Parse(result.StdOut, OpticalDiscType.Dvd);
            DiscTitle? single = info.Titles.FirstOrDefault();
            return single is null ? empty : single with { Index = titleIndex };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // DiscScanner.Parse wraps a JSON parse failure into
            // InvalidOperationException rather than letting JsonException
            // escape — this catch must widen to match, or a malformed
            // ffprobe response on a real disc crashes the per-title probe
            // instead of degrading to the empty skeleton title this method
            // otherwise always returns on failure.
            logger.LogInformation(
                ex,
                "DVD per-title probe parse failed for {Drive} title {Title}", [drive.Path, titleIndex]
            );
            return empty;
        }
    }

    /// <summary>
    /// libdvdcss-side error patterns. Most retail DVDs decrypt cleanly via
    /// libdvdcss; this only fires when the disc is region-locked outside
    /// the host's region or the CSS handshake actually fails.
    /// </summary>
    internal static DiscProtection? ClassifyProtection(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return null;

        if (
            stderr.Contains("css authentication failed", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(
                "could not get a key for any title",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return new(
                "CSS",
                null,
                "DVD CSS handshake failed. The drive's region may not match the disc's region, "
                    + "or libdvdcss could not derive a valid key from the disc."
            );
        }

        if (stderr.Contains("region code mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return new(
                "RegionLock",
                null,
                "DVD region does not match the drive region — change the drive region or use a region-free drive."
            );
        }

        return null;
    }

    private static string ToDvdPath(string mountPath)
    {
        // libdvdread's filesystem-fallback path needs to point at the
        // VIDEO_TS folder, not the disc root. Without it libdvdcss tries
        // raw block reads and fails on any drive that doesn't expose the
        // SCSI MMC interface (virtual drives, some USB enclosures).
        if (mountPath.StartsWith("dvd:", StringComparison.OrdinalIgnoreCase))
            return mountPath;
        string trimmed = mountPath.TrimEnd(['\\', '/']);
        return $"{trimmed}/VIDEO_TS/";
    }
}
