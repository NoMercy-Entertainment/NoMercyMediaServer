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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.LiveTranscode;

public class PlaybackDecisionEngine : IPlaybackDecisionEngine
{
    public PlaybackDecision[] DecideBatch(MediaInfo[] library, ClientCapabilities client) =>
        library.Select(selector: m => Decide(media: m, client: client)).ToArray();

    public PlaybackDecision Decide(MediaInfo media, ClientCapabilities client)
    {
        // Audio-only path
        if (!media.HasVideo)
        {
            if (media.HasAudio && IsAudioCompatible(audio: media.AudioStreams[index: 0], client: client))
                return new(Action: PlaybackAction.DirectPlay, Reason: null, DirectStreamUrl: null);

            return new(Action: PlaybackAction.TranscodeAudio, Reason: "Audio codec not supported", DirectStreamUrl: null);
        }

        VideoStreamInfo video = media.VideoStreams[index: 0];
        bool videoCodecOk = IsVideoCodecCompatible(video: video, client: client);
        bool audioCodecOk = !media.HasAudio || IsAudioCompatible(audio: media.AudioStreams[index: 0], client: client);
        bool containerOk = IsContainerCompatible(format: media.Format, client: client);
        bool resolutionOk = video.Width <= client.MaxWidth && video.Height <= client.MaxHeight;
        bool bitrateOk = client.MaxBitrateKbps <= 0 || video.BitRateKbps <= client.MaxBitrateKbps;
        bool hdrOk = !video.IsHdr || client.SupportsHdr;

        // A client that lists the codec (e.g. HEVC) but not 10-bit still cannot
        // decode a 10-bit stream. Without this gate a 10-bit HEVC source is
        // judged "codec compatible" and gets remuxed (copied) straight through,
        // handing the browser bytes it can't decode. Treat excess bit-depth
        // exactly like an unsupported codec — it forces a real transcode down
        // to 8-bit.
        bool bitDepthOk = video.BitDepth <= 8 || client.Supports10Bit;

        // Video needs transcode — codec, resolution, bit-depth, or HDR incompatible
        if (!videoCodecOk || !resolutionOk || !hdrOk || !bitDepthOk)
        {
            string reason =
                !videoCodecOk ? $"Client doesn't support {video.Codec}"
                : !resolutionOk
                    ? $"Resolution {video.Width}x{video.Height} exceeds client max {client.MaxWidth}x{client.MaxHeight}"
                : !hdrOk ? "Client doesn't support HDR"
                : $"Client doesn't support {video.BitDepth}-bit video";

            return new(Action: PlaybackAction.TranscodeVideo, Reason: reason, DirectStreamUrl: null);
        }

        // Video codec OK, but audio needs transcode
        if (!audioCodecOk)
            return new(Action: PlaybackAction.TranscodeAudio, Reason: "Audio codec not supported by client", DirectStreamUrl: null);

        // Video + audio OK, but container wrong → remux
        if (!containerOk)
            return new(
                Action: PlaybackAction.Remux,
                Reason: $"Container '{media.Format}' not supported, remuxing",
                DirectStreamUrl: null
            );

        // Everything else OK but bitrate too high → transcode to reduce
        if (!bitrateOk)
            return new(Action: PlaybackAction.TranscodeVideo, Reason: "Bitrate exceeds client limit", DirectStreamUrl: null);

        return new(Action: PlaybackAction.DirectPlay, Reason: null, DirectStreamUrl: null);
    }

    private static bool IsVideoCodecCompatible(VideoStreamInfo video, ClientCapabilities client)
    {
        VideoCodecType? sourceCodec = MapVideoCodec(codec: video.Codec);
        return sourceCodec.HasValue && client.SupportedVideoCodecs.Contains(value: sourceCodec.Value);
    }

    private static bool IsAudioCompatible(AudioStreamInfo audio, ClientCapabilities client)
    {
        AudioCodecType? sourceCodec = MapAudioCodec(codec: audio.Codec);
        return sourceCodec.HasValue && client.SupportedAudioCodecs.Contains(value: sourceCodec.Value);
    }

    private static bool IsContainerCompatible(string format, ClientCapabilities client)
    {
        string? container = MapContainer(format: format);
        return container is not null
            && client.SupportedContainers.Any(predicate: c =>
                c.Equals(value: container, comparisonType: StringComparison.OrdinalIgnoreCase)
            );
    }

    private static VideoCodecType? MapVideoCodec(string codec) =>
        codec.ToLowerInvariant() switch
        {
            "h264" => VideoCodecType.H264,
            "hevc" or "h265" => VideoCodecType.H265,
            "av1" => VideoCodecType.Av1,
            "vp9" => VideoCodecType.Vp9,
            _ => null,
        };

    private static AudioCodecType? MapAudioCodec(string codec) =>
        codec.ToLowerInvariant() switch
        {
            "aac" => AudioCodecType.Aac,
            "ac3" => AudioCodecType.Ac3,
            "eac3" => AudioCodecType.Eac3,
            "flac" => AudioCodecType.Flac,
            "opus" => AudioCodecType.Opus,
            "mp3" => AudioCodecType.Mp3,
            "vorbis" => AudioCodecType.Vorbis,
            "truehd" => AudioCodecType.TrueHd,
            "dts" or "dca" => AudioCodecType.Dts,
            _ => null,
        };

    private static string? MapContainer(string format) =>
        format.ToLowerInvariant() switch
        {
            string f when f.Contains(value: "matroska") => "mkv",
            string f when f.Contains(value: "mp4") || f.Contains(value: "mov") => "mp4",
            string f when f.Contains(value: "mpegts") => "ts",
            // ffprobe reports an HLS playlist as "hls,applehttp" (or "applehttp"),
            // never a bare "hls" — an exact match here silently missed every HLS
            // source and forced a needless remux session.
            string f when f.Contains(value: "hls") || f.Contains(value: "applehttp") => "hls",
            string f when f.Contains(value: "flac") => "flac",
            _ => null,
        };
}
