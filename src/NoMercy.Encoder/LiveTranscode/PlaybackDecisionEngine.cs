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
        library.Select(m => Decide(m, client)).ToArray();

    public PlaybackDecision Decide(MediaInfo media, ClientCapabilities client)
    {
        // Audio-only path
        if (!media.HasVideo)
        {
            if (media.HasAudio && IsAudioCompatible(media.AudioStreams[0], client))
                return new(PlaybackAction.DirectPlay, null, null);

            return new(PlaybackAction.TranscodeAudio, "Audio codec not supported", null);
        }

        VideoStreamInfo video = media.VideoStreams[0];
        bool videoCodecOk = IsVideoCodecCompatible(video, client);
        bool audioCodecOk = !media.HasAudio || IsAudioCompatible(media.AudioStreams[0], client);
        bool containerOk = IsContainerCompatible(media.Format, client);
        bool resolutionOk = video.Width <= client.MaxWidth && video.Height <= client.MaxHeight;
        bool bitrateOk = client.MaxBitrateKbps <= 0 || video.BitRateKbps <= client.MaxBitrateKbps;
        bool hdrOk = !video.IsHdr || client.SupportsHdr;

        // Video needs transcode — codec, resolution, or HDR incompatible
        if (!videoCodecOk || !resolutionOk || !hdrOk)
        {
            string reason =
                !videoCodecOk ? $"Client doesn't support {video.Codec}"
                : !resolutionOk
                    ? $"Resolution {video.Width}x{video.Height} exceeds client max {client.MaxWidth}x{client.MaxHeight}"
                : "Client doesn't support HDR";

            return new(PlaybackAction.TranscodeVideo, reason, null);
        }

        // Video codec OK, but audio needs transcode
        if (!audioCodecOk)
            return new(PlaybackAction.TranscodeAudio, "Audio codec not supported by client", null);

        // Video + audio OK, but container wrong → remux
        if (!containerOk)
            return new(
                PlaybackAction.Remux,
                $"Container '{media.Format}' not supported, remuxing",
                null
            );

        // Everything else OK but bitrate too high → transcode to reduce
        if (!bitrateOk)
            return new(PlaybackAction.TranscodeVideo, "Bitrate exceeds client limit", null);

        return new(PlaybackAction.DirectPlay, null, null);
    }

    private static bool IsVideoCodecCompatible(VideoStreamInfo video, ClientCapabilities client)
    {
        VideoCodecType? sourceCodec = MapVideoCodec(video.Codec);
        return sourceCodec.HasValue && client.SupportedVideoCodecs.Contains(sourceCodec.Value);
    }

    private static bool IsAudioCompatible(AudioStreamInfo audio, ClientCapabilities client)
    {
        AudioCodecType? sourceCodec = MapAudioCodec(audio.Codec);
        return sourceCodec.HasValue && client.SupportedAudioCodecs.Contains(sourceCodec.Value);
    }

    private static bool IsContainerCompatible(string format, ClientCapabilities client)
    {
        string? container = MapContainer(format);
        return container is not null && client.SupportedContainers.Contains(container);
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
            string f when f.Contains("matroska") => "mkv",
            string f when f.Contains("mp4") || f.Contains("mov") => "mp4",
            "mpegts" => "ts",
            "hls" => "hls",
            "flac" => "flac",
            _ => null,
        };
}
