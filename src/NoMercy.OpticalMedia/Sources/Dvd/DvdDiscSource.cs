using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public sealed partial class DvdDiscSource(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<DvdDiscSource> logger
) : IDiscSource
{
    public OpticalDiscType Type => OpticalDiscType.Dvd;

    public async Task<DiscInfo> ProbeAsync(DiscDrive drive, CancellationToken ct)
    {
        string drivePath = ToDvdPath(drive.Path);

        // Without -title libdvdread enumerates all titles in stderr. The
        // dvdvideo demuxer needs to be selected explicitly with -f.
        ProcessResult result = await processRunner.RunAsync(
            options.FfprobePath,
            ["-hide_banner", "-v", "info", "-f", "dvdvideo", "-i", drivePath],
            workingDirectory: null,
            cancellationToken: ct
        );

        DiscProtection? protection = ClassifyProtection(result.StdErr);
        List<(int Index, TimeSpan Duration, int Chapters)> titles = ParseTitles(result.StdErr);

        if (titles.Count == 0)
        {
            logger.LogInformation(
                "DVD probe parsed 0 titles for {Drive} | exit={Exit} stderr_len={StdErrLen} stderr_head={StdErrHead}",
                drive.Path,
                result.ExitCode,
                result.StdErr?.Length ?? 0,
                (result.StdErr ?? "").Length > 600
                    ? result.StdErr![..600]
                    : (result.StdErr ?? "(no stderr)")
            );
            return new DiscInfo(
                OpticalDiscType.Dvd,
                drive.Label,
                [],
                null,
                TimeSpan.Zero,
                protection
            );
        }

        TimeSpan maxDuration = titles.Max(t => t.Duration);
        DiscTitle[] discTitles = titles
            .Select(t => new DiscTitle(
                Index: t.Index,
                Name: $"Title {t.Index:D2}",
                Duration: t.Duration,
                VideoStreams: [],
                AudioStreams: [],
                Subtitles: [],
                Chapters: [],
                EstimatedSizeBytes: 0,
                IsMainFeature: t.Duration == maxDuration
            ))
            .OrderByDescending(t => t.Duration)
            .ToArray();

        return new DiscInfo(
            Type: OpticalDiscType.Dvd,
            DiscLabel: drive.Label,
            Titles: discTitles,
            AudioTracks: null,
            TotalDuration: discTitles.Sum(t => t.Duration.Ticks) is long ticks
                ? TimeSpan.FromTicks(ticks)
                : TimeSpan.Zero,
            Protection: protection
        );
    }

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
        catch (JsonException ex)
        {
            logger.LogInformation(
                ex,
                "DVD per-title probe parse failed for {Drive} title {Title}",
                drive.Path,
                titleIndex
            );
            return empty;
        }
    }

    /// <summary>
    /// Pulls title lines out of libdvdread's stderr. The demuxer prints one
    /// line per title with index, runtime, chapter count. Pattern:
    ///   <c>[dvdvideo @ ...] title 1, runtime 1:42:31, chapters 28</c>
    /// (exact format may vary between fork builds; the regex tolerates
    /// optional whitespace and varied separators).
    /// </summary>
    internal static List<(int Index, TimeSpan Duration, int Chapters)> ParseTitles(string stderr)
    {
        List<(int, TimeSpan, int)> titles = new();
        if (string.IsNullOrEmpty(stderr))
            return titles;

        foreach (Match match in TitleRegex().Matches(stderr))
        {
            string indexText = match.Groups["index"].Value;
            string durText = match.Groups["duration"].Value;
            string chaptersText = match.Groups["chapters"].Value;

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
            int chapters = 0;
            if (!string.IsNullOrEmpty(chaptersText))
                int.TryParse(
                    chaptersText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out chapters
                );

            titles.Add((idx, dur, chapters));
        }

        return titles.DistinctBy(t => t.Item1).ToList();
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
            return new DiscProtection(
                Kind: "CSS",
                VolumeId: null,
                Message: "DVD CSS handshake failed. The drive's region may not match the disc's region, "
                    + "or libdvdcss could not derive a valid key from the disc."
            );
        }

        if (stderr.Contains("region code mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return new DiscProtection(
                Kind: "RegionLock",
                VolumeId: null,
                Message: "DVD region does not match the drive region — change the drive region or use a region-free drive."
            );
        }

        return null;
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

    private static string ToDvdPath(string mountPath)
    {
        // The dvdvideo demuxer accepts a plain mount path (with trailing
        // separator) or a "dvd:" URL — both work. Plain path is most
        // portable, no protocol-handler dependency.
        if (mountPath.StartsWith("dvd:", StringComparison.OrdinalIgnoreCase))
            return mountPath;
        string trimmed = mountPath.TrimEnd('\\', '/');
        return $"{trimmed}/";
    }

    [GeneratedRegex(
        @"title\s+(?<index>\d+)[,:\s]+(?:runtime|length|duration)?\s*(?<duration>\d{1,}:\d{1,}:\d{1,})(?:[,\s]+chapters?\s+(?<chapters>\d+))?",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex TitleRegex();
}
