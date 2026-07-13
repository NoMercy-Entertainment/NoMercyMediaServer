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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.LiveTranscode;

/// <summary>
/// Builds the ffmpeg argv for one live-transcode HLS session. A pure function
/// of <see cref="LiveRunInput"/> — reuses the VOD command-building layer's
/// building blocks (<see cref="EncoderArgumentResolver"/> for preset
/// validation, <see cref="ProfileRuleValidator.ReservedFlags"/> for the
/// CustomArguments escape hatch) instead of re-deriving encoder constants.
/// </summary>
internal static class LiveFfmpegArgumentBuilder
{
    // Low-latency preset per encoder family. VOD's PresetResolver defaults to
    // the encoder's MIDDLE preset (quality/speed balance for an offline job);
    // live transcoding must keep up with real-time playback, so it needs a
    // fast preset even at the cost of some compression efficiency.
    private static readonly Dictionary<string, string> LivePresetByEncoder = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["libx264"] = "veryfast",
        ["libx265"] = "veryfast",
        ["h264_nvenc"] = "p4",
        ["hevc_nvenc"] = "p4",
        ["h264_amf"] = "speed",
        ["hevc_amf"] = "speed",
        ["h264_qsv"] = "veryfast",
        ["hevc_qsv"] = "veryfast",
    };

    private static readonly CodecRegistry Registry = new();

    public static string[] Build(LiveRunInput input)
    {
        string playlist = Path.Combine(input.OutputDirectory, LiveFfmpegRunner.PlaylistFileName);
        string segmentPattern = Path.Combine(
            input.OutputDirectory,
            $"{LiveFfmpegRunner.SegmentPrefix}%05d.ts"
        );

        List<string> args = ["-hide_banner", "-nostats", "-loglevel", "error"];

        if (input.StartPosition > TimeSpan.Zero)
        {
            args.Add("-ss");
            args.Add(FormatSeconds(input.StartPosition.TotalSeconds));
        }

        args.Add("-i");
        args.Add(input.InputPath);

        bool hasVideo = input.SourceInfo is null || input.SourceInfo.HasVideo;
        PlaybackAction action = DetermineAction(input, hasVideo);

        if (hasVideo)
            AppendVideo(args, input, action);
        else
            args.Add("-vn");

        AppendAudio(args, input, action);

        AppendHls(args, input, segmentPattern);

        if (input.CustomArguments is { Count: > 0 })
            AppendCustomArguments(args, input.CustomArguments);

        args.Add(playlist);

        args.Add("-progress");
        args.Add("pipe:1");

        return [.. args];
    }

    /// <summary>
    /// Video-less sources always need an audio transcode (DirectPlay never
    /// reaches the live encoder, so a video-less source landing here is by
    /// definition audio-incompatible). When either the client capabilities or
    /// the source analysis is missing, there isn't enough context to evaluate
    /// remux/copy eligibility — default to a full video transcode, the same
    /// behaviour this runner used unconditionally before this branch existed.
    /// </summary>
    private static PlaybackAction DetermineAction(LiveRunInput input, bool hasVideo)
    {
        if (!hasVideo)
            return PlaybackAction.TranscodeAudio;

        if (input.SourceInfo is null || input.Client is null)
            return PlaybackAction.TranscodeVideo;

        PlaybackDecision decision = new PlaybackDecisionEngine().Decide(
            input.SourceInfo,
            input.Client
        );

        // DirectPlay never reaches the live encoder in practice — LiveTranscodeService
        // short-circuits it before a session starts. Treat it defensively as a full
        // transcode rather than emitting a stream copy for a decision this layer
        // never expects to see.
        return decision.Action == PlaybackAction.DirectPlay
            ? PlaybackAction.TranscodeVideo
            : decision.Action;
    }

    private static void AppendVideo(List<string> args, LiveRunInput input, PlaybackAction action)
    {
        args.Add("-map");
        args.Add("0:v:0");

        // Remux and TranscodeAudio both mean the video codec is already
        // client-compatible — only the container or the audio track needs to
        // change, so re-encoding video would burn CPU for zero benefit. This is
        // the large CPU saving for the common "H.264-in-MKV, browser wants
        // fMP4/TS" case.
        if (action is PlaybackAction.Remux or PlaybackAction.TranscodeAudio)
        {
            args.Add("-c:v");
            args.Add("copy");
            return;
        }

        LiveQuality quality = input.Quality;

        args.Add("-c:v");
        args.Add(quality.Encoder);

        args.Add("-b:v");
        args.Add($"{quality.BitrateKbps}k");

        string? preset = ResolveLivePreset(quality.Encoder);
        if (preset is not null)
        {
            args.Add("-preset");
            args.Add(preset);
        }

        // VBV cap around the ABR target so segment sizes stay predictable for
        // HLS clients instead of ballooning on complex scenes.
        int maxrateKbps = (int)Math.Round(quality.BitrateKbps * 1.5);
        int bufsizeKbps = quality.BitrateKbps * 2;
        args.Add("-maxrate");
        args.Add($"{maxrateKbps}k");
        args.Add("-bufsize");
        args.Add($"{bufsizeKbps}k");

        args.Add("-vf");
        args.Add(BuildVideoFilterChain(input));

        AppendGop(args, input);
    }

    /// <summary>
    /// Resolves a low-latency preset for the encoder, validated against the
    /// encoder's actual preset vocabulary via the same
    /// <see cref="EncoderArgumentResolver"/> the VOD pipeline uses. Returns
    /// null for encoders with no preset concept (VAAPI, VideoToolbox) or an
    /// unrecognised encoder name.
    /// </summary>
    private static string? ResolveLivePreset(string encoderName)
    {
        string? requested = LivePresetByEncoder.GetValueOrDefault(encoderName);
        EncoderInfo? encoderInfo = Registry.GetVideoEncoderByName(encoderName);

        return encoderInfo is not null
            ? EncoderArgumentResolver.ResolvePreset(requested, encoderInfo)
            : requested;
    }

    private static string BuildVideoFilterChain(LiveRunInput input)
    {
        bool needsTonemap =
            input.Client is { SupportsHdr: false } && (input.SourceInfo?.IsHdr ?? false);

        string vfChain = $"scale={input.Quality.Width}:{input.Quality.Height}";

        // Browser-safe 8-bit: a browser's software/H.264 decode path cannot
        // play High-10/Main10. Force yuv420p on EVERY transcoded output, not
        // only the HDR→SDR tonemap chain — without this, a 10-bit HEVC SDR
        // source silently encodes as High-10 and simply fails to decode.
        vfChain += needsTonemap
            ? ",zscale=t=linear:npl=100,format=gbrpf32le"
                + ",zscale=p=bt709"
                + ",tonemap=tonemap=hable:desat=0"
                + ",zscale=t=bt709:m=bt709:r=tv"
                + ",format=yuv420p"
            : ",format=yuv420p";

        return vfChain;
    }

    /// <summary>
    /// Aligns keyframes to HLS segment boundaries so every segment is
    /// independently decodable. <c>-force_key_frames</c> guarantees an IDR at
    /// each segment start regardless of <c>-g</c>; <c>-g</c> mirrors the VOD
    /// HLS strategy's ceiling (2× the segment's frame count) as a backstop so
    /// the encoder's own GOP structure never drifts past two segments without
    /// a keyframe.
    /// </summary>
    private static void AppendGop(List<string> args, LiveRunInput input)
    {
        double frameRate =
            input.SourceInfo?.VideoStreams.Count > 0
                ? input.SourceInfo.VideoStreams[0].FrameRate
                : 24;
        int segmentDuration = input.SegmentDurationSeconds;

        args.Add("-force_key_frames");
        args.Add($"expr:gte(t,n_forced*{segmentDuration})");
        args.Add("-forced-idr");
        args.Add("1");

        int gopCeiling = (int)Math.Ceiling(frameRate * segmentDuration * 2);
        args.Add("-g");
        args.Add(gopCeiling.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendAudio(List<string> args, LiveRunInput input, PlaybackAction action)
    {
        args.Add("-map");
        args.Add("0:a:0?");

        if (action == PlaybackAction.Remux)
        {
            args.Add("-c:a");
            args.Add("copy");
            return;
        }

        // Clamp audio channel count to what the client supports.
        int sourceChannels =
            input.SourceInfo?.AudioStreams.Count > 0
                ? input.SourceInfo.AudioStreams[0].Channels
                : 2;
        int clientMax = input.Client?.MaxAudioChannels is > 0 ? input.Client.MaxAudioChannels : 2;
        int outputChannels = Math.Min(sourceChannels, clientMax);

        args.Add("-c:a");
        args.Add("aac");
        args.Add("-b:a");
        args.Add("128k");
        args.Add("-ac");
        args.Add(outputChannels.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendHls(List<string> args, LiveRunInput input, string segmentPattern)
    {
        args.Add("-f");
        args.Add("hls");
        args.Add("-hls_time");
        args.Add(input.SegmentDurationSeconds.ToString(CultureInfo.InvariantCulture));
        args.Add("-hls_list_size");
        args.Add("0");
        args.Add("-hls_playlist_type");
        args.Add("event");
        args.Add("-hls_flags");
        args.Add("independent_segments+temp_file");
        args.Add("-hls_segment_filename");
        args.Add(segmentPattern);
    }

    // Profile/plugin CustomArguments escape hatch — merged last so author
    // intent wins, mirroring PlanStage's per-video merge. Any key already
    // owned by a typed field above is skipped so the argv can never emit a
    // duplicate/conflicting flag next to the value this builder computed.
    private static void AppendCustomArguments(
        List<string> args,
        IReadOnlyDictionary<string, string> customArguments
    )
    {
        foreach ((string key, string value) in customArguments)
        {
            string normalizedKey = key.StartsWith('-') ? key : $"-{key}";
            if (ProfileRuleValidator.ReservedFlags.Contains(normalizedKey))
                continue;

            args.Add(normalizedKey);
            if (value.Length > 0)
                args.Add(value);
        }
    }

    private static string FormatSeconds(double seconds) =>
        seconds.ToString("F3", CultureInfo.InvariantCulture);
}
