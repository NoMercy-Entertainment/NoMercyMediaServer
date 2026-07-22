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
        string drivePath = ToDvdPath(mountPath: drive.Path);

        // libdvdread doesn't enumerate titles in a single call — it expects
        // the caller to walk titles 1..N sequentially. We probe each title
        // with -show_format until ffprobe emits "Title N not found" on
        // stderr, which is the enumeration boundary.
        List<DiscTitle> titles = new();
        DiscProtection? protection = null;
        for (int titleIdx = 1; titleIdx <= MaxTitleProbes; titleIdx++)
        {
            ProcessResult result = await processRunner.RunAsync(
                executable: options.FfprobePath,
                arguments:
                [
                    "-hide_banner",
                    "-v",
                    "error",
                    "-f",
                    "dvdvideo",
                    "-title",
                    titleIdx.ToString(provider: CultureInfo.InvariantCulture),
                    "-print_format",
                    "json",
                    "-show_format",
                    "-i",
                    drivePath,
                ],
                workingDirectory: null,
                cancellationToken: ct
            );

            // Capture protection state from the first probe — same stderr
            // signatures every iteration anyway.
            protection ??= ClassifyProtection(stderr: result.StdErr);

            if (
                !string.IsNullOrEmpty(value: result.StdErr)
                && result.StdErr.Contains(
                    value: $"Title {titleIdx} not found",
                    comparisonType: StringComparison.OrdinalIgnoreCase
                )
            )
            {
                // Boundary hit — stop walking.
                break;
            }

            if (string.IsNullOrWhiteSpace(value: result.StdOut))
                continue;

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json: result.StdOut);
                if (!doc.RootElement.TryGetProperty(propertyName: "format", value: out JsonElement format))
                    continue;

                TimeSpan duration = TimeSpan.Zero;
                if (
                    format.TryGetProperty(propertyName: "duration", value: out JsonElement dur)
                    && double.TryParse(
                        s: dur.GetString(),
                        style: NumberStyles.Float,
                        provider: CultureInfo.InvariantCulture,
                        result: out double seconds
                    )
                )
                {
                    duration = TimeSpan.FromSeconds(value: seconds);
                }

                titles.Add(
                    item: new(
                        Index: titleIdx,
                        Name: $"Title {titleIdx:D2}",
                        Duration: duration,
                        VideoStreams: [],
                        AudioStreams: [],
                        Subtitles: [],
                        Chapters: [],
                        EstimatedSizeBytes: 0,
                        IsMainFeature: false
                    )
                );
            }
            catch (JsonException ex)
            {
                logger.LogInformation(
                    exception: ex,
                    message: "DVD title {Title} probe parse failed for {Drive}", args: [titleIdx, drive.Path]
                );
            }
        }

        if (titles.Count == 0)
        {
            logger.LogInformation(
                message: "DVD probe found 0 titles for {Drive}: {Protection}", args: [drive.Path, protection?.Message ?? "(no protection detected)"]
            );
            return new(Type: OpticalDiscType.Dvd, DiscLabel: drive.Label, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero, Protection: protection);
        }

        // Flag the longest title as the main feature.
        TimeSpan maxDuration = titles.Max(selector: t => t.Duration);
        DiscTitle[] flagged = titles
            .Select(selector: t => t with { IsMainFeature = t.Duration == maxDuration })
            .OrderByDescending(keySelector: t => t.Duration)
            .ToArray();

        return new(
            Type: OpticalDiscType.Dvd,
            DiscLabel: drive.Label,
            Titles: flagged,
            AudioTracks: null,
            TotalDuration: flagged.Sum(selector: t => t.Duration.Ticks) is long ticks
                ? TimeSpan.FromTicks(value: ticks)
                : TimeSpan.Zero,
            Protection: protection
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
        string drivePath = ToDvdPath(mountPath: drive.Path);

        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments:
            [
                "-v",
                "quiet",
                "-f",
                "dvdvideo",
                "-title",
                titleIndex.ToString(provider: CultureInfo.InvariantCulture),
                "-print_format",
                "json",
                "-show_format",
                "-show_streams",
                "-show_chapters",
                "-i",
                drivePath,
            ],
            workingDirectory: null,
            cancellationToken: ct
        );

        DiscTitle empty = new(
            Index: titleIndex,
            Name: $"Title {titleIndex:D2}",
            Duration: TimeSpan.Zero,
            VideoStreams: [],
            AudioStreams: [],
            Subtitles: [],
            Chapters: [],
            EstimatedSizeBytes: 0,
            IsMainFeature: false
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
            return empty;

        try
        {
            // Re-use Bluray's parser — same JSON shape from ffprobe regardless
            // of input demuxer. Returns DiscInfo with one title; we lift that
            // title out and re-stamp the index.
            DiscInfo info = Bluray.DiscScanner.Parse(json: result.StdOut, discType: OpticalDiscType.Dvd);
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
                exception: ex,
                message: "DVD per-title probe parse failed for {Drive} title {Title}", args: [drive.Path, titleIndex]
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
        if (string.IsNullOrEmpty(value: stderr))
            return null;

        if (
            stderr.Contains(value: "css authentication failed", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(
                value: "could not get a key for any title",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return new(
                Kind: "CSS",
                VolumeId: null,
                Message: "DVD CSS handshake failed. The drive's region may not match the disc's region, "
                    + "or libdvdcss could not derive a valid key from the disc."
            );
        }

        if (stderr.Contains(value: "region code mismatch", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return new(
                Kind: "RegionLock",
                VolumeId: null,
                Message: "DVD region does not match the drive region — change the drive region or use a region-free drive."
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
        if (mountPath.StartsWith(value: "dvd:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return mountPath;
        string trimmed = mountPath.TrimEnd(trimChars: ['\\', '/']);
        return $"{trimmed}/VIDEO_TS/";
    }
}
