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

namespace NoMercy.Encoder.Profiles;

using Codecs;

public static class ContainerCompatibility
{
    private static readonly Dictionary<Container, HashSet<VideoCodecType>> VideoMatrix = new()
    {
        [key: Container.Mkv] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
        [key: Container.Mp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        // Apple HLS Authoring Specification §1.5: HEVC MUST use fMP4. Same
        // applies to AV1 (CMAF). Only H.264 may use MPEG-TS segments.
        [key: Container.HlsTs] = [VideoCodecType.H264],
        [key: Container.HlsFmp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        [key: Container.Dash] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
    };

    private static readonly Dictionary<Container, HashSet<AudioCodecType>> AudioMatrix = new()
    {
        [key: Container.Mkv] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Mp3,
            AudioCodecType.Opus,
            AudioCodecType.Flac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.TrueHd,
            AudioCodecType.Dts,
            AudioCodecType.Vorbis,
        ],
        [key: Container.Mp4] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        [key: Container.HlsTs] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        // fMP4 HLS/CMAF carries Opus fine (the nomercy-ffmpeg fork muxes it into
        // fragmented MP4 without error — verified against a real ffmpeg run),
        // it just isn't part of the narrower CmafAudio set below, which tracks
        // Apple's own HLS Authoring Spec recommendation rather than raw muxer
        // capability.
        [key: Container.HlsFmp4] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Opus,
        ],
        [key: Container.Mp3] = [AudioCodecType.Mp3],
        [key: Container.Aac] = [AudioCodecType.Aac],
        [key: Container.Flac] = [AudioCodecType.Flac],
        [key: Container.Ogg] = [AudioCodecType.Vorbis, AudioCodecType.Opus, AudioCodecType.Flac],
        [key: Container.Mka] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Mp3,
            AudioCodecType.Opus,
            AudioCodecType.Flac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.TrueHd,
            AudioCodecType.Dts,
            AudioCodecType.Vorbis,
        ],
        [key: Container.AudioHlsTs] = [AudioCodecType.Aac, AudioCodecType.Mp3],
        [key: Container.AudioHlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
        [key: Container.Dash] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
    };

    private static readonly Dictionary<Container, HashSet<SubtitleCodecType>> SubtitleMatrix = new()
    {
        // MKV carries every text + image subtitle codec NoMercy can produce.
        [key: Container.Mkv] =
        [
            SubtitleCodecType.WebVtt,
            SubtitleCodecType.Srt,
            SubtitleCodecType.Ass,
            SubtitleCodecType.Pgs,
            SubtitleCodecType.Copy,
        ],
        // MP4 supports TX3G text tracks (Srt → tx3g) and WebVTT (mp4 wvtt sample entry). PGS
        // bitmap subs and ASS typesetting are MKV-only.
        [key: Container.Mp4] = [SubtitleCodecType.WebVtt, SubtitleCodecType.Srt, SubtitleCodecType.Copy],
        // HLS carries WebVTT as sidecar subtitle renditions. Burned-in tracks don't appear here.
        [key: Container.HlsTs] = [SubtitleCodecType.WebVtt],
        [key: Container.HlsFmp4] = [SubtitleCodecType.WebVtt],
        [key: Container.Dash] = [SubtitleCodecType.WebVtt],
        // Audio-only containers do not carry subtitle tracks.
        [key: Container.Mp3] = [],
        [key: Container.Aac] = [],
        [key: Container.Flac] = [],
        [key: Container.Ogg] = [],
        [key: Container.Mka] = [],
        [key: Container.AudioHlsTs] = [],
        [key: Container.AudioHlsFmp4] = [],
    };

    private static readonly HashSet<VideoCodecType> CmafVideo =
    [
        VideoCodecType.H264,
        VideoCodecType.H265,
        VideoCodecType.Av1,
    ];
    private static readonly HashSet<AudioCodecType> CmafAudio =
    [
        AudioCodecType.Aac,
        AudioCodecType.Eac3,
    ];

    public static bool SupportsVideo(Container container, VideoCodecType codec) =>
        VideoMatrix.TryGetValue(key: container, value: out HashSet<VideoCodecType>? set) && set.Contains(item: codec);

    public static bool SupportsAudio(Container container, AudioCodecType codec) =>
        AudioMatrix.TryGetValue(key: container, value: out HashSet<AudioCodecType>? set) && set.Contains(item: codec);

    public static bool SupportsSubtitle(Container container, SubtitleCodecType codec) =>
        SubtitleMatrix.TryGetValue(key: container, value: out HashSet<SubtitleCodecType>? set)
        && set.Contains(item: codec);

    public static bool IsCmafCompatible(VideoCodecType codec) => CmafVideo.Contains(item: codec);

    public static bool IsCmafCompatible(AudioCodecType codec) => CmafAudio.Contains(item: codec);
}
