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
using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Storage;

namespace NoMercy.Encoder.Analysis;

public class MediaAnalyzer(IProcessRunner processRunner, IStorage storage, EncoderOptions options)
    : IMediaAnalyzer
{
    private static readonly string[] FfprobeArgs =
    [
        "-v",
        "quiet",
        "-print_format",
        "json",
        "-show_format",
        "-show_streams",
        "-show_chapters",
    ];

    public async Task<MediaInfo> AnalyzeAsync(string filePath, CancellationToken ct = default) =>
        await AnalyzeAsync(filePath: filePath, sourceStorage: storage, ct: ct);

    public async Task<MediaInfo> AnalyzeAsync(
        string filePath,
        string[]? extraInputArgs,
        CancellationToken ct = default
    )
    {
        bool isUrl =
            filePath.StartsWith(value: "http://", comparisonType: StringComparison.OrdinalIgnoreCase)
            || filePath.StartsWith(value: "https://", comparisonType: StringComparison.OrdinalIgnoreCase);

        // Non-URL source: keep the scope-validated, remote-staging filesystem path.
        // Extra input args are only meaningful for the HTTP self-ingest URL.
        if (!isUrl)
            return await AnalyzeAsync(filePath: filePath, sourceStorage: storage, ct: ct);

        string[] arguments = [.. FfprobeArgs, .. (extraInputArgs ?? []), filePath];
        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments: arguments,
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                message: $"ffprobe failed for '{filePath}': {result.StdErr}"
            );

        return ParseFfprobeJson(json: result.StdOut, filePath: filePath);
    }

    public async Task<MediaInfo> AnalyzeAsync(
        string filePath,
        IStorage sourceStorage,
        CancellationToken ct = default
    )
    {
        await using LocalPathLease inputLease = sourceStorage.AcquireLocalPath(path: filePath);
        string[] arguments = [.. FfprobeArgs, inputLease.Path];
        ProcessResult result = await processRunner.RunAsync(
            executable: options.FfprobePath,
            arguments: arguments,
            workingDirectory: null,
            cancellationToken: ct
        );

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                message: $"ffprobe failed for '{filePath}': {result.StdErr}"
            );

        return ParseFfprobeJson(json: result.StdOut, filePath: filePath);
    }

    internal static MediaInfo ParseFfprobeJson(string json, string filePath)
    {
        // ffprobe occasionally produces partial output on a damaged container
        // (truncated mkv tail, EAGAIN mid-write). Surface that as an
        // InvalidOperationException with the file path attached so the
        // caller can log meaningfully instead of dropping a raw
        // JsonReaderException with no context.
        JObject root;
        try
        {
            root = JObject.Parse(json: json);
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            throw new InvalidOperationException(
                message: $"ffprobe output for '{filePath}' was not valid JSON: {ex.Message}",
                innerException: ex
            );
        }
        JArray streams = root[propertyName: "streams"] as JArray ?? [];
        JArray chapters = root[propertyName: "chapters"] as JArray ?? [];
        JObject format = root[propertyName: "format"] as JObject ?? new JObject();

        List<VideoStreamInfo> videoStreams = [];
        List<AudioStreamInfo> audioStreams = [];
        List<SubtitleStreamInfo> subtitleStreams = [];
        List<AttachmentInfo> attachments = [];

        foreach (JToken stream in streams)
        {
            string codecType = stream.Value<string>(key: "codec_type") ?? "";
            switch (codecType)
            {
                case "video":
                    videoStreams.Add(item: ParseVideoStream(stream: stream));
                    break;
                case "audio":
                    audioStreams.Add(item: ParseAudioStream(stream: stream));
                    break;
                case "subtitle":
                    subtitleStreams.Add(item: ParseSubtitleStream(stream: stream));
                    break;
                case "attachment":
                    attachments.Add(
                        item: new(
                            Index: stream.Value<int>(key: "index"),
                            Codec: stream.Value<string>(key: "codec_name") ?? "unknown",
                            Filename: stream[key: "tags"]?.Value<string>(key: "filename"),
                            MimeType: stream[key: "tags"]?.Value<string>(key: "mimetype")
                        )
                    );
                    break;
            }
        }

        List<ChapterInfo> chapterList = [];
        foreach (JToken chapter in chapters)
        {
            chapterList.Add(
                item: new(
                    Start: TimeSpan.FromSeconds(value: chapter.Value<double>(key: "start_time")),
                    End: TimeSpan.FromSeconds(value: chapter.Value<double>(key: "end_time")),
                    Title: chapter[key: "tags"]?.Value<string>(key: "title")
                )
            );
        }

        double durationSeconds = format.Value<double>(key: "duration");
        long bitRate = ParseLong(token: format, key: "bit_rate");
        long fileSize = ParseLong(token: format, key: "size");
        string formatName = format.Value<string>(key: "format_name") ?? "unknown";

        DolbyVisionInfo? dolbyVision = ParseDolbyVision(streams: streams);
        string? stereoMode = ParseStereoMode(streams: streams);
        string? sphericalProjection = ParseSphericalProjection(streams: streams);

        return new(
            FilePath: filePath,
            Format: formatName,
            Duration: TimeSpan.FromSeconds(value: durationSeconds),
            OverallBitRateKbps: bitRate / 1000,
            FileSizeBytes: fileSize,
            VideoStreams: videoStreams,
            AudioStreams: audioStreams,
            SubtitleStreams: subtitleStreams,
            Chapters: chapterList,
            Attachments: attachments,
            DolbyVision: dolbyVision,
            StereoMode: stereoMode,
            SphericalProjection: sphericalProjection,
            // Already parsed above — retained verbatim, never re-parsed, so
            // the reconstruction blueprint's source.ffprobe is byte-identical
            // to what ffprobe emitted.
            Ffprobe: root
        );
    }

    internal static string? ParseStereoMode(JArray streams)
    {
        foreach (JToken stream in streams)
        {
            if (stream.Value<string>(key: "codec_type") != "video")
                continue;

            string? tagValue = stream[key: "tags"]?.Value<string>(key: "stereo_mode");
            if (!string.IsNullOrEmpty(value: tagValue))
                return tagValue;

            JArray? sideData = stream[key: "side_data_list"] as JArray;
            if (sideData is null)
                continue;

            foreach (JToken entry in sideData)
            {
                string? sideDataType = entry.Value<string>(key: "side_data_type");
                if (
                    sideDataType is null
                    || !sideDataType.Contains(value: "Stereo 3D", comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                    continue;

                string? mode = entry.Value<string>(key: "type");
                if (!string.IsNullOrEmpty(value: mode))
                    return mode;
            }
        }

        return null;
    }

    internal static string? ParseSphericalProjection(JArray streams)
    {
        foreach (JToken stream in streams)
        {
            if (stream.Value<string>(key: "codec_type") != "video")
                continue;

            JArray? sideData = stream[key: "side_data_list"] as JArray;
            if (sideData is null)
                continue;

            foreach (JToken entry in sideData)
            {
                string? sideDataType = entry.Value<string>(key: "side_data_type");
                if (
                    sideDataType is null
                    || !sideDataType.Contains(
                        value: "Spherical Mapping",
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                )
                    continue;

                string? projection = entry.Value<string>(key: "projection");
                if (!string.IsNullOrEmpty(value: projection))
                    return projection;
            }
        }

        return null;
    }

    private static VideoStreamInfo ParseVideoStream(JToken stream)
    {
        string pixFmt = stream.Value<string>(key: "pix_fmt") ?? "yuv420p";
        int bitDepth = ParseBitDepth(stream: stream, pixFmt: pixFmt);
        double frameRate = ParseFrameRate(frac: stream.Value<string>(key: "r_frame_rate") ?? "24/1");
        double? avgFrameRate = ParseNullableFrameRate(frac: stream.Value<string>(key: "avg_frame_rate"));
        double? realFrameRate = ParseNullableFrameRate(frac: stream.Value<string>(key: "r_frame_rate"));

        return new(
            Index: stream.Value<int>(key: "index"),
            Codec: stream.Value<string>(key: "codec_name") ?? "unknown",
            Width: stream.Value<int>(key: "width"),
            Height: stream.Value<int>(key: "height"),
            FrameRate: frameRate,
            BitDepth: bitDepth,
            PixelFormat: pixFmt,
            ColorPrimaries: stream.Value<string>(key: "color_primaries"),
            ColorTransfer: stream.Value<string>(key: "color_transfer"),
            ColorSpace: stream.Value<string>(key: "color_space"),
            IsDefault: stream[key: "disposition"]?.Value<int>(key: "default") == 1,
            BitRateKbps: ParseLong(token: stream, key: "bit_rate") / 1000,
            AverageFrameRate: avgFrameRate,
            RealFrameRate: realFrameRate,
            FieldOrder: stream.Value<string>(key: "field_order"),
            SampleAspectRatio: stream.Value<string>(key: "sample_aspect_ratio")
        );
    }

    /// <summary>
    /// Walks every video stream's <c>side_data_list</c> looking for the
    /// "DOVI configuration record" entry that ffprobe emits for Dolby Vision
    /// content. Returns <c>null</c> when no DV record is present. Only the
    /// first DV-bearing stream is reported — a single file never has more
    /// than one DV profile in practice.
    /// </summary>
    internal static DolbyVisionInfo? ParseDolbyVision(JArray streams)
    {
        foreach (JToken stream in streams)
        {
            if (stream.Value<string>(key: "codec_type") != "video")
                continue;

            JArray? sideData = stream[key: "side_data_list"] as JArray;
            if (sideData is null)
                continue;

            foreach (JToken entry in sideData)
            {
                string? type = entry.Value<string>(key: "side_data_type");
                // ffprobe uses two spellings depending on how DV is muxed:
                // "DOVI configuration record" (MP4/MKV container) and
                // "Dolby Vision Metadata" (in-stream RPU).
                if (
                    type is null
                    || (
                        !type.Contains(value: "DOVI", comparisonType: StringComparison.OrdinalIgnoreCase)
                        && !type.Contains(value: "Dolby Vision", comparisonType: StringComparison.OrdinalIgnoreCase)
                    )
                )
                    continue;

                int profile = entry.Value<int?>(key: "dv_profile") ?? 0;
                int level = entry.Value<int?>(key: "dv_level") ?? 0;
                bool hasRpu = entry.Value<int?>(key: "rpu_present_flag") == 1;
                bool hasEl = entry.Value<int?>(key: "el_present_flag") == 1;
                int compatId = entry.Value<int?>(key: "dv_bl_signal_compatibility_id") ?? 0;

                DvBlCompatibility compat = compatId switch
                {
                    1 => DvBlCompatibility.Hdr10,
                    2 => DvBlCompatibility.Sdr,
                    _ => DvBlCompatibility.None,
                };

                return new(Profile: profile, Level: level, HasRpu: hasRpu, HasEl: hasEl, BlCompat: compat);
            }
        }

        return null;
    }

    private static AudioStreamInfo ParseAudioStream(JToken stream)
    {
        return new(
            Index: stream.Value<int>(key: "index"),
            Codec: stream.Value<string>(key: "codec_name") ?? "unknown",
            Channels: stream.Value<int>(key: "channels"),
            SampleRate: stream.Value<int>(key: "sample_rate"),
            BitRateKbps: ParseLong(token: stream, key: "bit_rate") / 1000,
            Language: stream[key: "tags"]?.Value<string>(key: "language"),
            IsDefault: stream[key: "disposition"]?.Value<int>(key: "default") == 1,
            IsForced: stream[key: "disposition"]?.Value<int>(key: "forced") == 1
        );
    }

    private static SubtitleStreamInfo ParseSubtitleStream(JToken stream)
    {
        return new(
            Index: stream.Value<int>(key: "index"),
            Codec: stream.Value<string>(key: "codec_name") ?? "unknown",
            Language: stream[key: "tags"]?.Value<string>(key: "language"),
            IsDefault: stream[key: "disposition"]?.Value<int>(key: "default") == 1,
            IsForced: stream[key: "disposition"]?.Value<int>(key: "forced") == 1,
            Title: stream[key: "tags"]?.Value<string>(key: "title")
        );
    }

    private static int ParseBitDepth(JToken stream, string pixFmt)
    {
        string? bitsRaw = stream.Value<string>(key: "bits_per_raw_sample");
        if (bitsRaw is not null && int.TryParse(s: bitsRaw, result: out int bits))
            return bits;
        return pixFmt.Contains(value: "10") ? 10 : 8;
    }

    private static double ParseFrameRate(string frac)
    {
        string[] parts = frac.Split(separator: '/');
        if (
            parts.Length == 2
            && double.TryParse(
                s: parts[0],
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double num
            )
            && double.TryParse(
                s: parts[1],
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double den
            )
            && den > 0
        )
            return num / den;
        return 24.0;
    }

    private static double? ParseNullableFrameRate(string? frac)
    {
        if (frac is null)
            return null;
        string[] parts = frac.Split(separator: '/');
        if (
            parts.Length == 2
            && double.TryParse(
                s: parts[0],
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double num
            )
            && double.TryParse(
                s: parts[1],
                style: NumberStyles.Float,
                provider: CultureInfo.InvariantCulture,
                result: out double den
            )
            && den > 0
        )
            return num / den;
        return null;
    }

    private static long ParseLong(JToken token, string key)
    {
        string? val = token.Value<string>(key: key);
        return val is not null && long.TryParse(s: val, result: out long result) ? result : 0;
    }
}
