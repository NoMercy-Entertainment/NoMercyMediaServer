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
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;

namespace NoMercy.OpticalMedia.Sources.Bluray;

/// <summary>
/// Enumerates titles on a Blu-ray or DVD by invoking FFprobe against the
/// disc's libbluray / libdvdread pseudo-URL. Returns a <see cref="DiscInfo"/>
/// with every playable title the user can then select for ripping.
///
/// Drive path conventions:
/// - Linux Blu-ray: <c>bluray:/dev/sr0</c>
/// - Linux DVD:     <c>/dev/sr0</c> (ffprobe auto-detects the VIDEO_TS tree)
/// - Windows:       <c>bluray:D:</c> or <c>D:\</c>
///
/// The caller passes whichever form the OS yielded — the scanner doesn't
/// rewrite paths. <see cref="IDriveMonitor"/> produces usable paths.
/// </summary>
public class DiscScanner(
    EncoderOptions options,
    IProcessRunner processRunner,
    ILogger<DiscScanner> logger
)
{
    public async Task<DiscInfo> ScanAsync(string drivePath, CancellationToken ct)
    {
        OpticalDiscType discType = drivePath.StartsWith(
            value: "bluray:",
            comparisonType: StringComparison.OrdinalIgnoreCase
        )
            ? OpticalDiscType.BluRay
            : OpticalDiscType.Dvd;

        // For Blu-ray drives, run a 1-second ffprobe pre-scan to detect AACS /
        // BD+ failures before attempting the full (potentially slow) scan.
        if (discType == OpticalDiscType.BluRay)
        {
            using CancellationTokenSource probeCts =
                CancellationTokenSource.CreateLinkedTokenSource(token: ct);
            probeCts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 1));

            try
            {
                ProcessResult preProbe = await processRunner.RunAsync(
                    executable: options.FfprobePath,
                    arguments: ["-v", "quiet", "-show_format", drivePath],
                    workingDirectory: null,
                    cancellationToken: probeCts.Token
                );

                if (!preProbe.IsSuccess)
                    ClassifyBluRayStderr(drivePath: drivePath, stderr: preProbe.StdErr);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 1-second timeout expired — process still running means the disc
                // responded (just slowly). Let the real scan proceed.
            }
        }

        string[] args =
        [
            "-v",
            "quiet",
            "-print_format",
            "json",
            "-show_format",
            "-show_streams",
            "-show_chapters",
            drivePath,
        ];

        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments: args,
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(value: result.StdOut))
        {
            logger.LogWarning(
                message: "Disc scan failed for {Drive} (exit {Exit}): {Stderr}", args: [drivePath, result.ExitCode, TrimStderr(stdErr: result.StdErr)]
            );
            return new(Type: discType, DiscLabel: null, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        }

        try
        {
            return Parse(json: result.StdOut, discType: discType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(exception: ex, message: "Failed to parse disc scan output for {Drive}", args: drivePath);
            return new(Type: discType, DiscLabel: null, Titles: [], AudioTracks: null, TotalDuration: TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Parses the ffprobe JSON envelope. For a Blu-ray, ffprobe's default
    /// output is the "longest" title only — the scanner reports just that
    /// one title for now. Multi-title enumeration requires libbluray's
    /// playlist dump, which is a separate tool-chain step; a V2 can add it.
    /// </summary>
    internal static DiscInfo Parse(string json, OpticalDiscType discType)
    {
        // ffprobe on a partially-readable disc occasionally returns truncated
        // output — surface that as InvalidOperationException so the caller
        // can log meaningfully instead of letting JsonException leak out.
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json: json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                message: $"ffprobe output was not valid JSON: {ex.Message}",
                innerException: ex
            );
        }
        using JsonDocument _ = doc;
        JsonElement root = doc.RootElement;

        string? discLabel = null;
        TimeSpan duration = TimeSpan.Zero;
        if (root.TryGetProperty(propertyName: "format", value: out JsonElement format))
        {
            if (
                format.TryGetProperty(propertyName: "tags", value: out JsonElement tags)
                && tags.TryGetProperty(propertyName: "title", value: out JsonElement titleElement)
            )
            {
                discLabel = titleElement.GetString();
            }
            if (
                format.TryGetProperty(propertyName: "duration", value: out JsonElement durationElement)
                && double.TryParse(
                    s: durationElement.GetString(),
                    style: NumberStyles.Float,
                    provider: CultureInfo.InvariantCulture,
                    result: out double seconds
                )
            )
            {
                duration = TimeSpan.FromSeconds(value: seconds);
            }
        }

        List<VideoStreamInfo> videoStreams = [];
        List<AudioStreamInfo> audioStreams = [];
        List<SubtitleStreamInfo> subtitles = [];
        if (root.TryGetProperty(propertyName: "streams", value: out JsonElement streams))
        {
            foreach (JsonElement stream in streams.EnumerateArray())
            {
                string codecType = stream.TryGetProperty(propertyName: "codec_type", value: out JsonElement t)
                    ? (t.GetString() ?? "")
                    : "";

                switch (codecType)
                {
                    case "video":
                        videoStreams.Add(item: ParseVideo(stream: stream));
                        break;
                    case "audio":
                        audioStreams.Add(item: ParseAudio(stream: stream));
                        break;
                    case "subtitle":
                        subtitles.Add(item: ParseSubtitle(stream: stream));
                        break;
                }
            }
        }

        List<ChapterInfo> chapters = [];
        if (root.TryGetProperty(propertyName: "chapters", value: out JsonElement chaptersElement))
        {
            foreach (JsonElement chapter in chaptersElement.EnumerateArray())
            {
                chapters.Add(item: ParseChapter(chapter: chapter));
            }
        }

        DiscTitle singleTitle = new(
            Index: 0,
            Name: discLabel,
            Duration: duration,
            VideoStreams: videoStreams.ToArray(),
            AudioStreams: audioStreams.ToArray(),
            Subtitles: subtitles.ToArray(),
            Chapters: chapters.ToArray(),
            EstimatedSizeBytes: 0,
            IsMainFeature: true
        );

        return new(Type: discType, DiscLabel: discLabel, Titles: [singleTitle], AudioTracks: null, TotalDuration: duration);
    }

    private static VideoStreamInfo ParseVideo(JsonElement stream) =>
        new(
            Index: stream.TryGetProperty(propertyName: "index", value: out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty(propertyName: "codec_name", value: out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Width: stream.TryGetProperty(propertyName: "width", value: out JsonElement w) ? w.GetInt32() : 0,
            Height: stream.TryGetProperty(propertyName: "height", value: out JsonElement h) ? h.GetInt32() : 0,
            FrameRate: 0,
            BitDepth: 8,
            PixelFormat: stream.TryGetProperty(propertyName: "pix_fmt", value: out JsonElement px)
                ? (px.GetString() ?? "")
                : "",
            ColorPrimaries: null,
            ColorTransfer: null,
            ColorSpace: null,
            IsDefault: false,
            BitRateKbps: 0
        );

    private static AudioStreamInfo ParseAudio(JsonElement stream)
    {
        string? language = null;
        if (
            stream.TryGetProperty(propertyName: "tags", value: out JsonElement tags)
            && tags.TryGetProperty(propertyName: "language", value: out JsonElement lang)
        )
        {
            language = lang.GetString();
        }

        return new(
            Index: stream.TryGetProperty(propertyName: "index", value: out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty(propertyName: "codec_name", value: out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Channels: stream.TryGetProperty(propertyName: "channels", value: out JsonElement ch) ? ch.GetInt32() : 0,
            SampleRate: stream.TryGetProperty(propertyName: "sample_rate", value: out JsonElement sr)
            && int.TryParse(s: sr.GetString(), result: out int srInt)
                ? srInt
                : 0,
            BitRateKbps: 0,
            Language: language,
            IsDefault: false,
            IsForced: false
        );
    }

    private static SubtitleStreamInfo ParseSubtitle(JsonElement stream)
    {
        string? language = null;
        if (
            stream.TryGetProperty(propertyName: "tags", value: out JsonElement tags)
            && tags.TryGetProperty(propertyName: "language", value: out JsonElement lang)
        )
        {
            language = lang.GetString();
        }

        return new(
            Index: stream.TryGetProperty(propertyName: "index", value: out JsonElement i) ? i.GetInt32() : 0,
            Codec: stream.TryGetProperty(propertyName: "codec_name", value: out JsonElement c)
                ? (c.GetString() ?? "")
                : "",
            Language: language,
            IsDefault: false,
            IsForced: false
        );
    }

    private static ChapterInfo ParseChapter(JsonElement chapter)
    {
        double start = 0;
        double end = 0;
        if (
            chapter.TryGetProperty(propertyName: "start_time", value: out JsonElement s)
            && double.TryParse(
                s: s.GetString(),
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double startParsed
            )
        )
        {
            start = startParsed;
        }
        if (
            chapter.TryGetProperty(propertyName: "end_time", value: out JsonElement e)
            && double.TryParse(
                s: e.GetString(),
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double endParsed
            )
        )
        {
            end = endParsed;
        }

        string? title = null;
        if (
            chapter.TryGetProperty(propertyName: "tags", value: out JsonElement tags)
            && tags.TryGetProperty(propertyName: "title", value: out JsonElement t)
        )
        {
            title = t.GetString();
        }

        return new(Start: TimeSpan.FromSeconds(value: start), End: TimeSpan.FromSeconds(value: end), Title: title);
    }

    /// <summary>
    /// Inspects ffprobe stderr from a Blu-ray pre-probe and throws the
    /// appropriate <see cref="EncoderRuntimeException"/> when a known
    /// decryption failure pattern is detected.
    /// </summary>
    internal static void ClassifyBluRayStderr(string drivePath, string stderr)
    {
        if (string.IsNullOrEmpty(value: stderr))
            return;

        // libaacs emits this when KEYDB.cfg has no entry for the disc's volume ID.
        // Pattern observed in libaacs src/libaacs/aacs.c:
        //   "aacs: no matching certificate"  (older builds)
        //   "AACS: no matching certificate"  (case varies by build)
        if (
            stderr.Contains(value: "no matching certificate", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(value: "aacs:", comparisonType: StringComparison.OrdinalIgnoreCase)
                && stderr.Contains(value: "certificate", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            string volumeId = ExtractVolumeId(stderr: stderr) ?? drivePath;
            throw RuntimeErrors.DiscAacsCertMissing(volumeId: volumeId);
        }

        // libbdplus emits this when the converter database has no entry for the disc.
        // Pattern from libbdplus src/libbdplus/bdplus.c:
        //   "bdplus: no matching converter"
        if (stderr.Contains(value: "no matching converter", comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            string volumeId = ExtractVolumeId(stderr: stderr) ?? drivePath;
            throw RuntimeErrors.DiscBdplusConverterMissing(volumeId: volumeId);
        }

        // Any other protocol-level read failure.
        if (
            stderr.Contains(value: "Protocol not found", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(value: "No such file or directory", comparisonType: StringComparison.OrdinalIgnoreCase)
            || stderr.Contains(value: "Input/output error", comparisonType: StringComparison.OrdinalIgnoreCase)
        )
        {
            throw RuntimeErrors.DiscReadError(drivePath: drivePath, ffmpegStderrTail: TrimStderr(stdErr: stderr));
        }
    }

    /// <summary>
    /// Attempts to extract a volume/disc ID from ffprobe / libaacs stderr lines.
    /// libaacs typically logs the volume ID as a hex string on the error line.
    /// Returns null when no candidate is found.
    /// </summary>
    private static string? ExtractVolumeId(string stderr)
    {
        foreach (string line in stderr.Split(separator: '\n'))
        {
            // Look for a line containing a 32-char hex string — the AACS volume ID.
            Match m = Regex.Match(input: line, pattern: @"[0-9A-Fa-f]{32}");
            if (m.Success)
                return m.Value.ToUpperInvariant();
        }

        return null;
    }

    private static string TrimStderr(string stdErr)
    {
        if (string.IsNullOrEmpty(value: stdErr))
            return "(no stderr)";
        string[] lines = stdErr.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries);
        return lines.Length <= 3 ? stdErr : string.Join(separator: '\n', value: lines[^3..]);
    }
}
