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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.Storage;

namespace NoMercy.OpticalMedia.Sources.Bluray;

/// <summary>
/// Bluray disc reader. Uses nomercy-ffmpeg's <c>bluray:</c> protocol via
/// libbluray. <see cref="ProbeAsync"/> enumerates every usable playlist
/// the disc exposes by parsing libbluray's stderr playlist dump (cheap,
/// one ffprobe call). <see cref="ProbeTitleAsync"/> returns the detailed
/// streams + chapters for one playlist (slower, one ffprobe per call).
///
/// <see cref="ProbeAsync"/> also reads the disc-embedded title from
/// <c>BDMV/META/DL/bdmt_*.xml</c> (if present) and surfaces it in
/// <see cref="DiscInfo.DiscTitle"/>, which downstream identification
/// prefers over the raw volume label.
/// </summary>
public sealed partial class BlurayDiscSource(
    EncoderOptions options,
    IProcessRunner processRunner,
    IStorageDriver storageDriver,
    ILogger<BlurayDiscSource> logger
) : IDiscSource
{
    public OpticalDiscType Type => OpticalDiscType.BluRay;

    public async Task<DiscInfo> ProbeAsync(DiscDrive drive, CancellationToken ct)
    {
        string drivePath = ToBlurayUrl(mountPath: drive.Path);

        // libbluray dumps the full playlist set on stderr at -v info before
        // it commits to one. Run a thin probe just to capture that dump.
        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments: ["-hide_banner", "-v", "info", "-i", drivePath],
            workingDirectory: null,
            cancellationToken: ct
        );

        // Classify protection state from the stderr — even when the disc is
        // AACS-locked we may still have enumerable playlists (libbluray
        // reads BDMV structure without keys), so we attach Protection to
        // the DiscInfo rather than throwing.
        DiscProtection? protection = ClassifyProtection(stderr: result.StdErr);

        // Read the disc-embedded title from bdmt_*.xml before evaluating
        // playlists — we want to populate DiscTitle regardless of whether
        // any playlists are found.
        string? embeddedTitle = TryReadBdmtTitle(mountPath: drive.Path);

        // ffprobe always exits non-zero here (no input format chosen) — the
        // stderr is the payload we want regardless.
        List<(int Index, TimeSpan Duration)> playlists = ParsePlaylists(stderr: result.StdErr);
        if (playlists.Count == 0)
        {
            // Loud warning at INFO level so we always see it in the log when
            // a probe came back empty — separate from the per-message format
            // so existing log-grep filters don't swallow it.
            logger.LogInformation(
                message: "Bluray probe parsed 0 playlists for {Drive} | exit={Exit} stdout_len={StdOutLen} stderr_len={StdErrLen} stderr_head={StdErrHead}", args:
                [drive.Path, result.ExitCode, result.StdOut.Length, result.StdErr.Length, (result.StdErr ?? "").Length > 600
                    ? result.StdErr![..600]
                    : (result.StdErr ?? "(no stderr)")
                ]
            );
            return new(
                Type: OpticalDiscType.BluRay,
                DiscLabel: drive.Label,
                Titles: [],
                AudioTracks: null,
                TotalDuration: TimeSpan.Zero,
                Protection: protection,
                DiscTitle: embeddedTitle
            );
        }

        // Largest playlist by runtime is typically the disc's main feature
        // (movie disc) or the season-concat title (TV disc). Mark it so the
        // UI can highlight it as the default selection.
        TimeSpan maxDuration = playlists.Max(selector: p => p.Duration);
        DiscTitle[] titles = playlists
            .Select(selector: p => new DiscTitle(
                Index: p.Index,
                Name: $"Playlist {p.Index:D5}",
                Duration: p.Duration,
                VideoStreams: [],
                AudioStreams: [],
                Subtitles: [],
                Chapters: [],
                EstimatedSizeBytes: 0,
                IsMainFeature: p.Duration == maxDuration
            ))
            .OrderByDescending(keySelector: t => t.Duration)
            .ToArray();

        return new(
            Type: OpticalDiscType.BluRay,
            DiscLabel: drive.Label,
            Titles: titles,
            AudioTracks: null,
            TotalDuration: titles.Sum(selector: t => t.Duration.Ticks) is long ticks
                ? TimeSpan.FromTicks(value: ticks)
                : TimeSpan.Zero,
            Protection: protection,
            DiscTitle: embeddedTitle
        );
    }

    /// <summary>
    /// Inspects libaacs / libbdplus stderr for the well-known "I can't
    /// decrypt this disc" patterns. Returns null when nothing protection-y
    /// is in the output (fully readable or non-protected disc).
    /// </summary>
    internal static DiscProtection? ClassifyProtection(string stderr)
    {
        if (string.IsNullOrEmpty(value: stderr))
            return null;

        // libaacs SCSI MMC handshake fail (drive can't do AACS bus key)
        if (
            stderr.Contains(
                value: "Drive does not support reading drive certificate",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
            || stderr.Contains(
                value: "Unable to read drive certificate",
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return new(
                Kind: "AACS",
                VolumeId: null,
                Message: "Disc is AACS-protected and the optical drive can't establish "
                    + "the bus key required to read encrypted units. The drive's firmware "
                    + "likely lacks AACS-MKI-1 SCSI MMC support."
            );
        }

        // libaacs unable to derive VUK from KEYDB (no matching entry, or
        // entry present but key wrong)
        if (
            stderr.Contains(value: "Unable to decrypt unit (AACS)", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(value: "no matching certificate", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            return new(
                Kind: "AACS",
                VolumeId: null,
                Message: "Disc is AACS-protected but no matching key was found in KEYDB.cfg."
            );
        }

        // libbdplus
        if (stderr.Contains(value: "no matching converter", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return new(
                Kind: "BD+",
                VolumeId: null,
                Message: "Disc uses BD+ and the converter database has no entry for it."
            );
        }

        return null;
    }

    public async Task<DiscTitle> ProbeTitleAsync(
        DiscDrive drive,
        int titleIndex,
        CancellationToken ct
    )
    {
        string url = ToBlurayUrl(mountPath: drive.Path);
        DiscInfo info = await ScanWithPlaylistAsync(drivePath: url, playlistIndex: titleIndex, ct: ct);
        DiscTitle? single = info.Titles.FirstOrDefault();
        if (single is null)
            return new(
                Index: titleIndex,
                Name: $"Playlist {titleIndex:D5}",
                Duration: TimeSpan.Zero,
                VideoStreams: [],
                AudioStreams: [],
                Subtitles: [],
                Chapters: [],
                EstimatedSizeBytes: 0,
                IsMainFeature: false
            );

        return single with
        {
            Index = titleIndex,
        };
    }

    private async Task<DiscInfo> ScanWithPlaylistAsync(
        string drivePath,
        int playlistIndex,
        CancellationToken ct
    )
    {
        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments:
            [
                "-v",
                "quiet",
                "-playlist",
                playlistIndex.ToString(provider: CultureInfo.InvariantCulture),
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

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
        {
            logger.LogWarning(
                message: "Per-playlist probe failed for {Drive} #{Playlist} (exit {Exit}): {Stderr}", args: [drivePath, playlistIndex, result.ExitCode, TrimStderr(stdErr: result.StdErr)]
            );
            return new(Type: OpticalDiscType.BluRay, DiscLabel: null, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        }

        try
        {
            return DiscScanner.Parse(json: result.StdOut, discType: OpticalDiscType.BluRay);
        }
        catch (InvalidOperationException ex)
        {
            // DiscScanner.Parse wraps a JSON parse failure into
            // InvalidOperationException — a truncated/malformed ffprobe
            // response on a real disc must degrade to an empty probe result
            // here, the same way the !result.IsSuccess branch above does,
            // rather than crash the per-playlist detail fetch.
            logger.LogWarning(
                exception: ex,
                message: "Per-playlist probe returned unparsable JSON for {Drive} #{Playlist}", args: [drivePath, playlistIndex]
            );
            return new(Type: OpticalDiscType.BluRay, DiscLabel: null, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        }
    }

    internal static List<(int Index, TimeSpan Duration)> ParsePlaylists(string stderr)
    {
        List<(int, TimeSpan)> playlists = new();
        if (string.IsNullOrEmpty(value: stderr))
            return playlists;

        foreach (Match match in PlaylistRegex().Matches(input: stderr))
        {
            string indexText = match.Groups[groupname: "index"].Value;
            string durText = match.Groups[groupname: "duration"].Value;

            if (
                !int.TryParse(
                    s: indexText,
                    style: NumberStyles.Integer,
                    provider: CultureInfo.InvariantCulture,
                    result: out int idx
                )
            )
                continue;
            if (!TryParseHmsDuration(value: durText, dur: out TimeSpan dur))
                continue;

            playlists.Add(item: (idx, dur));
        }

        return playlists.DistinctBy(keySelector: p => p.Item1).ToList();
    }

    private static bool TryParseHmsDuration(string value, out TimeSpan dur)
    {
        dur = TimeSpan.Zero;
        string[] parts = value.Split(separator: ':');
        if (parts.Length != 3)
            return false;
        if (
            !int.TryParse(s: parts[0], style: NumberStyles.Integer, provider: CultureInfo.InvariantCulture, result: out int h)
            || !int.TryParse(
                s: parts[1],
                style: NumberStyles.Integer,
                provider: CultureInfo.InvariantCulture,
                result: out int m
            )
            || !int.TryParse(
                s: parts[2],
                style: NumberStyles.Integer,
                provider: CultureInfo.InvariantCulture,
                result: out int s
            )
        )
            return false;
        dur = new(hours: h, minutes: m, seconds: s);
        return true;
    }

    /// <summary>
    /// Attempts to read the human-readable disc title from Blu-ray
    /// <c>BDMV/META/DL/bdmt_*.xml</c>. Prefers the English variant
    /// (<c>bdmt_eng.xml</c>) and falls back to the first locale file
    /// found. Returns <c>null</c> when the disc has no embedded metadata
    /// or the xml cannot be parsed.
    /// </summary>
    internal string? TryReadBdmtTitle(string mountPath)
    {
        try
        {
            string trimmed = mountPath.TrimEnd(trimChars: ['\\', '/']);
            string dlDir = Path.Combine(path1: trimmed, path2: "BDMV", path3: "META", path4: "DL");

            if (!storageDriver.DirectoryExists(path: dlDir))
                return null;

            // Prefer English; fall back to the first locale present.
            string englishPath = Path.Combine(path1: dlDir, path2: "bdmt_eng.xml");
            string? xmlPath = storageDriver.FileExists(path: englishPath)
                ? englishPath
                : storageDriver
                    .EnumerateFileSystemEntries(directory: dlDir, searchPattern: "bdmt_*.xml", option: SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

            if (xmlPath is null || !storageDriver.FileExists(path: xmlPath))
                return null;

            using Stream stream = storageDriver.OpenRead(path: xmlPath);
            using StreamReader reader = new(stream: stream);
            string xmlContent = reader.ReadToEnd();
            XDocument doc = XDocument.Parse(text: xmlContent);
            XNamespace di = "urn:BDA:bdmv;discinfo";
            return doc.Descendants(name: di + "name").FirstOrDefault()?.Value;
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                exception: ex,
                message: "Could not read bdmt title from {Mount}: {Message}", args: [mountPath, ex.Message]
            );
            return null;
        }
    }

    private static string ToBlurayUrl(string mountPath)
    {
        if (mountPath.StartsWith(value: "bluray:", comparisonType: StringComparison.OrdinalIgnoreCase))
            return mountPath;
        // libbluray needs a trailing separator on Windows: "bluray:D:/" works,
        // "bluray:D:" never enumerates playlists.
        string trimmed = mountPath.TrimEnd(trimChars: ['\\', '/']);
        return $"bluray:{trimmed}/";
    }

    private static string TrimStderr(string stdErr)
    {
        if (string.IsNullOrEmpty(value: stdErr))
            return "(no stderr)";
        string[] lines = stdErr.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries);
        return lines.Length <= 3 ? stdErr : string.Join(separator: '\n', value: lines[^3..]);
    }

    [GeneratedRegex(
        pattern: @"playlist\s+(?<index>\d+)\.mpls\s+\((?<duration>\d{1,}:\d{1,}:\d{1,})\)",
        options: RegexOptions.IgnoreCase
    )]
    private static partial Regex PlaylistRegex();
}
