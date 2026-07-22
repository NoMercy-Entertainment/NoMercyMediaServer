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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class ContainerCompatibilityTests
{
    [Theory]
    [InlineData(data: [Container.Mp4, VideoCodecType.H264, true])]
    [InlineData(data: [Container.Mp4, VideoCodecType.H265, true])]
    [InlineData(data: [Container.Mp4, VideoCodecType.Vp9, false])]
    [InlineData(data: [Container.HlsTs, VideoCodecType.H264, true])]
    [InlineData(data: [Container.HlsTs, VideoCodecType.H265, false])]
    [InlineData(data: [Container.HlsTs, VideoCodecType.Av1, false])]
    [InlineData(data: [Container.HlsFmp4, VideoCodecType.H264, true])]
    [InlineData(data: [Container.HlsFmp4, VideoCodecType.H265, true])]
    [InlineData(data: [Container.HlsFmp4, VideoCodecType.Av1, true])]
    [InlineData(data: [Container.Mkv, VideoCodecType.H264, true])]
    [InlineData(data: [Container.Mkv, VideoCodecType.Av1, true])]
    public void Video_codec_compatibility(Container container, VideoCodecType codec, bool expected)
    {
        ContainerCompatibility.SupportsVideo(container: container, codec: codec).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [Container.Mp4, AudioCodecType.Aac, true])]
    [InlineData(data: [Container.Mp4, AudioCodecType.Opus, false])]
    [InlineData(data: [Container.HlsFmp4, AudioCodecType.Aac, true])]
    [InlineData(data: [Container.HlsFmp4, AudioCodecType.Eac3, true])]
    [InlineData(data: [Container.HlsFmp4, AudioCodecType.Opus, true])]
    [InlineData(data: [Container.AudioHlsFmp4, AudioCodecType.Opus, true])]
    [InlineData(data: [Container.Mkv, AudioCodecType.TrueHd, true])]
    [InlineData(data: [Container.Flac, AudioCodecType.Flac, true])]
    [InlineData(data: [Container.Mp3, AudioCodecType.Mp3, true])]
    public void Audio_codec_compatibility(Container container, AudioCodecType codec, bool expected)
    {
        ContainerCompatibility.SupportsAudio(container: container, codec: codec).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [VideoCodecType.H264, true])]
    [InlineData(data: [VideoCodecType.H265, true])]
    [InlineData(data: [VideoCodecType.Av1, true])]
    [InlineData(data: [VideoCodecType.Vp9, false])]
    public void Cmaf_compatible_video_codecs(VideoCodecType codec, bool expected)
    {
        ContainerCompatibility.IsCmafCompatible(codec: codec).Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [AudioCodecType.Aac, true])]
    [InlineData(data: [AudioCodecType.Eac3, true])]
    [InlineData(data: [AudioCodecType.Mp3, false])]
    [InlineData(data: [AudioCodecType.Opus, false])]
    [InlineData(data: [AudioCodecType.Flac, false])]
    public void Cmaf_compatible_audio_codecs(AudioCodecType codec, bool expected)
    {
        ContainerCompatibility.IsCmafCompatible(codec: codec).Should().Be(expected: expected);
    }

    [Fact]
    public void Full_video_compatibility_matrix_locks_contract()
    {
        Dictionary<(Container, VideoCodecType), bool> expected = BuildExpectedVideoMatrix();
        foreach ((Container container, VideoCodecType codec) in expected.Keys)
        {
            bool actual = ContainerCompatibility.SupportsVideo(container: container, codec: codec);
            actual
                .Should()
                .Be(
                    expected: expected[key: (container, codec)],
                    because: $"container {container} + codec {codec} expected {expected[key: (container, codec)]}"
                );
        }

        expected
            .Count.Should()
            .Be(
                expected: Enum.GetValues<Container>().Length * Enum.GetValues<VideoCodecType>().Length,
                because: "test fixture must enumerate every Container x VideoCodecType pair (forward-compat lock -- adding a codec forces explicit opt-in)"
            );
    }

    [Fact]
    public void Full_audio_compatibility_matrix_locks_contract()
    {
        Dictionary<(Container, AudioCodecType), bool> expected = BuildExpectedAudioMatrix();
        foreach ((Container container, AudioCodecType codec) in expected.Keys)
        {
            bool actual = ContainerCompatibility.SupportsAudio(container: container, codec: codec);
            actual
                .Should()
                .Be(
                    expected: expected[key: (container, codec)],
                    because: $"container {container} + codec {codec} expected {expected[key: (container, codec)]}"
                );
        }

        expected
            .Count.Should()
            .Be(
                expected: Enum.GetValues<Container>().Length * Enum.GetValues<AudioCodecType>().Length,
                because: "test fixture must enumerate every Container x AudioCodecType pair"
            );
    }

    [Fact]
    public void Audio_only_containers_reject_all_video_codecs()
    {
        Container[] audioOnly =
        [
            Container.Mp3,
            Container.Aac,
            Container.Flac,
            Container.Ogg,
            Container.Mka,
            Container.Mks,
            Container.AudioHlsTs,
            Container.AudioHlsFmp4,
        ];

        foreach (Container c in audioOnly)
        foreach (VideoCodecType v in Enum.GetValues<VideoCodecType>())
            ContainerCompatibility
                .SupportsVideo(container: c, codec: v)
                .Should()
                .BeFalse(because: $"{c} is audio-only but accepted {v}");
    }

    [Fact]
    public void Cmaf_compatible_video_full_enum()
    {
        foreach (VideoCodecType codec in Enum.GetValues<VideoCodecType>())
        {
            bool expected =
                codec is VideoCodecType.H264 or VideoCodecType.H265 or VideoCodecType.Av1;
            ContainerCompatibility
                .IsCmafCompatible(codec: codec)
                .Should()
                .Be(expected: expected, because: $"video codec {codec}");
        }
    }

    [Fact]
    public void Cmaf_compatible_audio_full_enum()
    {
        foreach (AudioCodecType codec in Enum.GetValues<AudioCodecType>())
        {
            bool expected = codec is AudioCodecType.Aac or AudioCodecType.Eac3;
            ContainerCompatibility
                .IsCmafCompatible(codec: codec)
                .Should()
                .Be(expected: expected, because: $"audio codec {codec}");
        }
    }

    [Theory]
    [InlineData(data: [Container.Mkv, SubtitleCodecType.WebVtt, true])]
    [InlineData(data: [Container.Mkv, SubtitleCodecType.Srt, true])]
    [InlineData(data: [Container.Mkv, SubtitleCodecType.Ass, true])]
    [InlineData(data: [Container.Mkv, SubtitleCodecType.Pgs, true])]
    [InlineData(data: [Container.Mp4, SubtitleCodecType.WebVtt, true])]
    [InlineData(data: [Container.Mp4, SubtitleCodecType.Srt, true])]
    [InlineData(data: [Container.Mp4, SubtitleCodecType.Ass, false])] // ASS is MKV-only
    [InlineData(data: [Container.Mp4, SubtitleCodecType.Pgs, false])] // PGS bitmap is MKV-only
    [InlineData(data: [Container.HlsTs, SubtitleCodecType.WebVtt, true])]
    [InlineData(data: [Container.HlsTs, SubtitleCodecType.Ass, false])]
    [InlineData(data: [Container.HlsFmp4, SubtitleCodecType.WebVtt, true])]
    [InlineData(data: [Container.Dash, SubtitleCodecType.WebVtt, true])]
    [InlineData(data: [Container.Mp3, SubtitleCodecType.WebVtt, false])] // audio-only
    [InlineData(data: [Container.Aac, SubtitleCodecType.WebVtt, false])]
    [InlineData(data: [Container.Flac, SubtitleCodecType.WebVtt, false])]
    [InlineData(data: [Container.Mka, SubtitleCodecType.WebVtt, false])]
    [InlineData(data: [Container.AudioHlsTs, SubtitleCodecType.WebVtt, false])]
    [InlineData(data: [Container.AudioHlsFmp4, SubtitleCodecType.WebVtt, false])]
    public void Subtitle_codec_compatibility(
        Container container,
        SubtitleCodecType codec,
        bool expected
    )
    {
        ContainerCompatibility.SupportsSubtitle(container: container, codec: codec).Should().Be(expected: expected);
    }

    [Fact]
    public void Audio_only_containers_reject_all_subtitle_codecs()
    {
        Container[] audioOnly =
        [
            Container.Mp3,
            Container.Aac,
            Container.Flac,
            Container.Ogg,
            Container.Mka,
            Container.AudioHlsTs,
            Container.AudioHlsFmp4,
        ];

        foreach (Container c in audioOnly)
        foreach (SubtitleCodecType s in Enum.GetValues<SubtitleCodecType>())
            ContainerCompatibility
                .SupportsSubtitle(container: c, codec: s)
                .Should()
                .BeFalse(because: $"{c} is audio-only but accepted subtitle codec {s}");
    }

    private static Dictionary<(Container, VideoCodecType), bool> BuildExpectedVideoMatrix()
    {
        Dictionary<(Container, VideoCodecType), bool> map = new();
        Container[] allContainers = Enum.GetValues<Container>();
        VideoCodecType[] allCodecs = Enum.GetValues<VideoCodecType>();

        // Copy is a remux pass-through, not a real encode codec — excluded from all containers.
        Dictionary<Container, HashSet<VideoCodecType>> truth = new()
        {
            [key: Container.Mkv] =
            [
                VideoCodecType.H264,
                VideoCodecType.H265,
                VideoCodecType.Av1,
                VideoCodecType.Vp9,
            ],
            [key: Container.Mp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
            [key: Container.HlsTs] = [VideoCodecType.H264],
            [key: Container.HlsFmp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
            [key: Container.Dash] =
            [
                VideoCodecType.H264,
                VideoCodecType.H265,
                VideoCodecType.Av1,
                VideoCodecType.Vp9,
            ],
            [key: Container.Mp3] = [],
            [key: Container.Aac] = [],
            [key: Container.Flac] = [],
            [key: Container.Ogg] = [],
            [key: Container.Mka] = [],
            [key: Container.Mks] = [],
            [key: Container.AudioHlsTs] = [],
            [key: Container.AudioHlsFmp4] = [],
        };

        foreach (Container c in allContainers)
        foreach (VideoCodecType v in allCodecs)
            map[key: (c, v)] = truth[key: c].Contains(item: v);

        return map;
    }

    private static Dictionary<(Container, AudioCodecType), bool> BuildExpectedAudioMatrix()
    {
        Dictionary<(Container, AudioCodecType), bool> map = new();
        Container[] allContainers = Enum.GetValues<Container>();
        AudioCodecType[] allCodecs = Enum.GetValues<AudioCodecType>();

        // Copy is a remux pass-through, not a real encode codec — excluded from all containers.
        Dictionary<Container, HashSet<AudioCodecType>> truth = new()
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
            [key: Container.Mks] = [],
            [key: Container.AudioHlsTs] = [AudioCodecType.Aac, AudioCodecType.Mp3],
            [key: Container.AudioHlsFmp4] =
            [
                AudioCodecType.Aac,
                AudioCodecType.Eac3,
                AudioCodecType.Opus,
            ],
            [key: Container.Dash] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
        };

        foreach (Container c in allContainers)
        foreach (AudioCodecType a in allCodecs)
            map[key: (c, a)] = truth[key: c].Contains(item: a);

        return map;
    }
}
