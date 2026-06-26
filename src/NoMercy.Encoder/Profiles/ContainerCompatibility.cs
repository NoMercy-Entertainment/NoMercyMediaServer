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
        [Container.Mkv] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
        [Container.Mp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        // Apple HLS Authoring Specification §1.5: HEVC MUST use fMP4. Same
        // applies to AV1 (CMAF). Only H.264 may use MPEG-TS segments.
        [Container.HlsTs] = [VideoCodecType.H264],
        [Container.HlsFmp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
        [Container.Dash] =
        [
            VideoCodecType.H264,
            VideoCodecType.H265,
            VideoCodecType.Av1,
            VideoCodecType.Vp9,
        ],
    };

    private static readonly Dictionary<Container, HashSet<AudioCodecType>> AudioMatrix = new()
    {
        [Container.Mkv] =
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
        [Container.Mp4] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        [Container.HlsTs] =
        [
            AudioCodecType.Aac,
            AudioCodecType.Ac3,
            AudioCodecType.Eac3,
            AudioCodecType.Mp3,
        ],
        [Container.HlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Ac3, AudioCodecType.Eac3],
        [Container.Mp3] = [AudioCodecType.Mp3],
        [Container.Aac] = [AudioCodecType.Aac],
        [Container.Flac] = [AudioCodecType.Flac],
        [Container.Ogg] = [AudioCodecType.Vorbis, AudioCodecType.Opus, AudioCodecType.Flac],
        [Container.Mka] =
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
        [Container.AudioHlsTs] = [AudioCodecType.Aac, AudioCodecType.Mp3],
        [Container.AudioHlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Eac3],
        [Container.Dash] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
    };

    private static readonly Dictionary<Container, HashSet<SubtitleCodecType>> SubtitleMatrix = new()
    {
        // MKV carries every text + image subtitle codec NoMercy can produce.
        [Container.Mkv] =
        [
            SubtitleCodecType.WebVtt,
            SubtitleCodecType.Srt,
            SubtitleCodecType.Ass,
            SubtitleCodecType.Pgs,
            SubtitleCodecType.Copy,
        ],
        // MP4 supports TX3G text tracks (Srt → tx3g) and WebVTT (mp4 wvtt sample entry). PGS
        // bitmap subs and ASS typesetting are MKV-only.
        [Container.Mp4] = [SubtitleCodecType.WebVtt, SubtitleCodecType.Srt, SubtitleCodecType.Copy],
        // HLS carries WebVTT as sidecar subtitle renditions. Burned-in tracks don't appear here.
        [Container.HlsTs] = [SubtitleCodecType.WebVtt],
        [Container.HlsFmp4] = [SubtitleCodecType.WebVtt],
        [Container.Dash] = [SubtitleCodecType.WebVtt],
        // Audio-only containers do not carry subtitle tracks.
        [Container.Mp3] = [],
        [Container.Aac] = [],
        [Container.Flac] = [],
        [Container.Ogg] = [],
        [Container.Mka] = [],
        [Container.AudioHlsTs] = [],
        [Container.AudioHlsFmp4] = [],
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
        VideoMatrix.TryGetValue(container, out HashSet<VideoCodecType>? set) && set.Contains(codec);

    public static bool SupportsAudio(Container container, AudioCodecType codec) =>
        AudioMatrix.TryGetValue(container, out HashSet<AudioCodecType>? set) && set.Contains(codec);

    public static bool SupportsSubtitle(Container container, SubtitleCodecType codec) =>
        SubtitleMatrix.TryGetValue(container, out HashSet<SubtitleCodecType>? set)
        && set.Contains(codec);

    public static bool IsCmafCompatible(VideoCodecType codec) => CmafVideo.Contains(codec);

    public static bool IsCmafCompatible(AudioCodecType codec) => CmafAudio.Contains(codec);
}
