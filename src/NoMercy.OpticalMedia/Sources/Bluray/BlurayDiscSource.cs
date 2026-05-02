using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;

namespace NoMercy.OpticalMedia.Sources.Bluray;

/// <summary>
/// Bluray disc reader. Uses nomercy-ffmpeg's <c>bluray:</c> protocol via
/// libbluray. <see cref="ProbeAsync"/> enumerates every usable playlist
/// the disc exposes by parsing libbluray's stderr playlist dump (cheap,
/// one ffprobe call). <see cref="ProbeTitleAsync"/> returns the detailed
/// streams + chapters for one playlist (slower, one ffprobe per call).
/// </summary>
public sealed partial class BlurayDiscSource(
    EncoderOptions options,
    IProcessRunner processRunner,
    IDiscScanner scanner,
    ILogger<BlurayDiscSource> logger
) : IDiscSource
{
    public OpticalDiscType Type => OpticalDiscType.BluRay;

    public async Task<DiscInfo> ProbeAsync(DiscDrive drive, CancellationToken ct)
    {
        string drivePath = ToBlurayUrl(drive.Path);

        // libbluray dumps the full playlist set on stderr at -v info before
        // it commits to one. Run a thin probe just to capture that dump.
        ProcessResult result = await processRunner.RunAsync(
            options.FfprobePath,
            ["-hide_banner", "-v", "info", "-i", drivePath],
            workingDirectory: null,
            cancellationToken: ct
        );

        // Classify protection state from the stderr — even when the disc is
        // AACS-locked we may still have enumerable playlists (libbluray
        // reads BDMV structure without keys), so we attach Protection to
        // the DiscInfo rather than throwing.
        DiscProtection? protection = ClassifyProtection(result.StdErr);

        // ffprobe always exits non-zero here (no input format chosen) — the
        // stderr is the payload we want regardless.
        List<(int Index, TimeSpan Duration)> playlists = ParsePlaylists(result.StdErr);
        if (playlists.Count == 0)
        {
            // Loud warning at INFO level so we always see it in the log when
            // a probe came back empty — separate from the per-message format
            // so existing log-grep filters don't swallow it.
            logger.LogInformation(
                "Bluray probe parsed 0 playlists for {Drive} | exit={Exit} stdout_len={StdOutLen} stderr_len={StdErrLen} stderr_head={StdErrHead}",
                drive.Path,
                result.ExitCode,
                result.StdOut?.Length ?? 0,
                result.StdErr?.Length ?? 0,
                (result.StdErr ?? "").Length > 600
                    ? result.StdErr![..600]
                    : (result.StdErr ?? "(no stderr)")
            );
            return new DiscInfo(
                OpticalDiscType.BluRay,
                drive.Label,
                [],
                null,
                TimeSpan.Zero,
                protection
            );
        }

        // Largest playlist by runtime is typically the disc's main feature
        // (movie disc) or the season-concat title (TV disc). Mark it so the
        // UI can highlight it as the default selection.
        TimeSpan maxDuration = playlists.Max(p => p.Duration);
        DiscTitle[] titles = playlists
            .Select(p => new DiscTitle(
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
            .OrderByDescending(t => t.Duration)
            .ToArray();

        return new DiscInfo(
            Type: OpticalDiscType.BluRay,
            DiscLabel: drive.Label,
            Titles: titles,
            AudioTracks: null,
            TotalDuration: titles.Sum(t => t.Duration.Ticks) is long ticks
                ? TimeSpan.FromTicks(ticks)
                : TimeSpan.Zero,
            Protection: protection
        );
    }

    /// <summary>
    /// Inspects libaacs / libbdplus stderr for the well-known "I can't
    /// decrypt this disc" patterns. Returns null when nothing protection-y
    /// is in the output (fully readable or non-protected disc).
    /// </summary>
    internal static DiscProtection? ClassifyProtection(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return null;

        // libaacs SCSI MMC handshake fail (drive can't do AACS bus key)
        if (
            stderr.Contains(
                "Drive does not support reading drive certificate",
                StringComparison.OrdinalIgnoreCase
            )
            || stderr.Contains(
                "Unable to read drive certificate",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return new DiscProtection(
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
            stderr.Contains("Unable to decrypt unit (AACS)", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("no matching certificate", StringComparison.OrdinalIgnoreCase)
        )
        {
            return new DiscProtection(
                Kind: "AACS",
                VolumeId: null,
                Message: "Disc is AACS-protected but no matching key was found in KEYDB.cfg."
            );
        }

        // libbdplus
        if (stderr.Contains("no matching converter", StringComparison.OrdinalIgnoreCase))
        {
            return new DiscProtection(
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
        // The existing IDiscScanner only knows the main playlist — feed it
        // a -playlist-scoped URL so its detailed parser sees just that one.
        string url = ToBlurayUrl(drive.Path);
        DiscInfo info = await ScanWithPlaylistAsync(url, titleIndex, ct);
        DiscTitle? single = info.Titles.FirstOrDefault();
        if (single is null)
            return new DiscTitle(
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

    /// <summary>
    /// Runs ffprobe with <c>-playlist {N}</c> and the JSON pipeline. Uses
    /// the existing <see cref="DiscScanner"/> code path so AACS/BD+ error
    /// classification and JSON parsing stay in one place.
    /// </summary>
    private async Task<DiscInfo> ScanWithPlaylistAsync(
        string drivePath,
        int playlistIndex,
        CancellationToken ct
    )
    {
        ProcessResult result = await processRunner.RunAsync(
            options.FfprobePath,
            [
                "-v",
                "quiet",
                "-playlist",
                playlistIndex.ToString(CultureInfo.InvariantCulture),
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

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
        {
            logger.LogWarning(
                "Per-playlist probe failed for {Drive} #{Playlist} (exit {Exit}): {Stderr}",
                drivePath,
                playlistIndex,
                result.ExitCode,
                TrimStderr(result.StdErr)
            );
            return new DiscInfo(OpticalDiscType.BluRay, null, [], null, TimeSpan.Zero);
        }

        return DiscScanner.Parse(result.StdOut, OpticalDiscType.BluRay);
    }

    internal static List<(int Index, TimeSpan Duration)> ParsePlaylists(string stderr)
    {
        List<(int, TimeSpan)> playlists = new();
        if (string.IsNullOrEmpty(stderr))
            return playlists;

        foreach (Match match in PlaylistRegex().Matches(stderr))
        {
            string indexText = match.Groups["index"].Value;
            string durText = match.Groups["duration"].Value;

            if (
                !int.TryParse(
                    indexText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int idx
                )
            )
                continue;
            if (!TryParseHmsDuration(durText, out TimeSpan dur))
                continue;

            playlists.Add((idx, dur));
        }

        return playlists.DistinctBy(p => p.Item1).ToList();
    }

    private static bool TryParseHmsDuration(string value, out TimeSpan dur)
    {
        dur = TimeSpan.Zero;
        string[] parts = value.Split(':');
        if (parts.Length != 3)
            return false;
        if (
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)
            || !int.TryParse(
                parts[1],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int m
            )
            || !int.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int s
            )
        )
            return false;
        dur = new TimeSpan(h, m, s);
        return true;
    }

    private static string ToBlurayUrl(string mountPath)
    {
        if (mountPath.StartsWith("bluray:", StringComparison.OrdinalIgnoreCase))
            return mountPath;
        // libbluray needs a trailing separator on Windows: "bluray:D:/" works,
        // "bluray:D:" never enumerates playlists.
        string trimmed = mountPath.TrimEnd('\\', '/');
        return $"bluray:{trimmed}/";
    }

    private static string TrimStderr(string stdErr)
    {
        if (string.IsNullOrEmpty(stdErr))
            return "(no stderr)";
        string[] lines = stdErr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length <= 3 ? stdErr : string.Join('\n', lines[^3..]);
    }

    [GeneratedRegex(
        @"playlist\s+(?<index>\d+)\.mpls\s+\((?<duration>\d{1,}:\d{1,}:\d{1,})\)",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex PlaylistRegex();
}
