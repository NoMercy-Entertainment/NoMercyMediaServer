using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

public class ContainerCompatibilityTests
{
    [Theory]
    [InlineData(Container.Mp4, VideoCodecType.H264, true)]
    [InlineData(Container.Mp4, VideoCodecType.H265, true)]
    [InlineData(Container.Mp4, VideoCodecType.Vp9, false)]
    [InlineData(Container.HlsTs, VideoCodecType.H264, true)]
    [InlineData(Container.HlsTs, VideoCodecType.H265, true)]
    [InlineData(Container.HlsTs, VideoCodecType.Av1, true)]
    [InlineData(Container.HlsFmp4, VideoCodecType.H264, true)]
    [InlineData(Container.HlsFmp4, VideoCodecType.H265, true)]
    [InlineData(Container.HlsFmp4, VideoCodecType.Av1, true)]
    [InlineData(Container.Mkv, VideoCodecType.H264, true)]
    [InlineData(Container.Mkv, VideoCodecType.Av1, true)]
    public void Video_codec_compatibility(Container container, VideoCodecType codec, bool expected)
    {
        ContainerCompatibility.SupportsVideo(container, codec).Should().Be(expected);
    }

    [Theory]
    [InlineData(Container.Mp4, AudioCodecType.Aac, true)]
    [InlineData(Container.Mp4, AudioCodecType.Opus, false)]
    [InlineData(Container.HlsFmp4, AudioCodecType.Aac, true)]
    [InlineData(Container.HlsFmp4, AudioCodecType.Eac3, true)]
    [InlineData(Container.Mkv, AudioCodecType.TrueHd, true)]
    [InlineData(Container.Flac, AudioCodecType.Flac, true)]
    [InlineData(Container.Mp3, AudioCodecType.Mp3, true)]
    public void Audio_codec_compatibility(Container container, AudioCodecType codec, bool expected)
    {
        ContainerCompatibility.SupportsAudio(container, codec).Should().Be(expected);
    }

    [Theory]
    [InlineData(VideoCodecType.H264, true)]
    [InlineData(VideoCodecType.H265, true)]
    [InlineData(VideoCodecType.Av1, true)]
    [InlineData(VideoCodecType.Vp9, false)]
    public void Cmaf_compatible_video_codecs(VideoCodecType codec, bool expected)
    {
        ContainerCompatibility.IsCmafCompatible(codec).Should().Be(expected);
    }

    [Theory]
    [InlineData(AudioCodecType.Aac, true)]
    [InlineData(AudioCodecType.Eac3, true)]
    [InlineData(AudioCodecType.Mp3, false)]
    [InlineData(AudioCodecType.Opus, false)]
    [InlineData(AudioCodecType.Flac, false)]
    public void Cmaf_compatible_audio_codecs(AudioCodecType codec, bool expected)
    {
        ContainerCompatibility.IsCmafCompatible(codec).Should().Be(expected);
    }

    [Fact]
    public void Full_video_compatibility_matrix_locks_contract()
    {
        Dictionary<(Container, VideoCodecType), bool> expected = BuildExpectedVideoMatrix();
        foreach ((Container container, VideoCodecType codec) in expected.Keys)
        {
            bool actual = ContainerCompatibility.SupportsVideo(container, codec);
            actual
                .Should()
                .Be(
                    expected[(container, codec)],
                    $"container {container} + codec {codec} expected {expected[(container, codec)]}"
                );
        }

        expected
            .Count.Should()
            .Be(
                Enum.GetValues<Container>().Length * Enum.GetValues<VideoCodecType>().Length,
                "test fixture must enumerate every Container x VideoCodecType pair (forward-compat lock -- adding a codec forces explicit opt-in)"
            );
    }

    [Fact]
    public void Full_audio_compatibility_matrix_locks_contract()
    {
        Dictionary<(Container, AudioCodecType), bool> expected = BuildExpectedAudioMatrix();
        foreach ((Container container, AudioCodecType codec) in expected.Keys)
        {
            bool actual = ContainerCompatibility.SupportsAudio(container, codec);
            actual
                .Should()
                .Be(
                    expected[(container, codec)],
                    $"container {container} + codec {codec} expected {expected[(container, codec)]}"
                );
        }

        expected
            .Count.Should()
            .Be(
                Enum.GetValues<Container>().Length * Enum.GetValues<AudioCodecType>().Length,
                "test fixture must enumerate every Container x AudioCodecType pair"
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
                .SupportsVideo(c, v)
                .Should()
                .BeFalse($"{c} is audio-only but accepted {v}");
    }

    [Fact]
    public void Cmaf_compatible_video_full_enum()
    {
        foreach (VideoCodecType codec in Enum.GetValues<VideoCodecType>())
        {
            bool expected =
                codec is VideoCodecType.H264 or VideoCodecType.H265 or VideoCodecType.Av1;
            ContainerCompatibility
                .IsCmafCompatible(codec)
                .Should()
                .Be(expected, $"video codec {codec}");
        }
    }

    [Fact]
    public void Cmaf_compatible_audio_full_enum()
    {
        foreach (AudioCodecType codec in Enum.GetValues<AudioCodecType>())
        {
            bool expected = codec is AudioCodecType.Aac or AudioCodecType.Eac3;
            ContainerCompatibility
                .IsCmafCompatible(codec)
                .Should()
                .Be(expected, $"audio codec {codec}");
        }
    }

    private static Dictionary<(Container, VideoCodecType), bool> BuildExpectedVideoMatrix()
    {
        Dictionary<(Container, VideoCodecType), bool> map = new();
        Container[] allContainers = Enum.GetValues<Container>();
        VideoCodecType[] allCodecs = Enum.GetValues<VideoCodecType>();

        // Copy is a remux pass-through, not a real encode codec — excluded from all containers.
        Dictionary<Container, HashSet<VideoCodecType>> truth = new()
        {
            [Container.Mkv] =
            [
                VideoCodecType.H264,
                VideoCodecType.H265,
                VideoCodecType.Av1,
                VideoCodecType.Vp9,
            ],
            [Container.Mp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
            [Container.HlsTs] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
            [Container.HlsFmp4] = [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1],
            [Container.Dash] =
            [
                VideoCodecType.H264,
                VideoCodecType.H265,
                VideoCodecType.Av1,
                VideoCodecType.Vp9,
            ],
            [Container.Mp3] = [],
            [Container.Aac] = [],
            [Container.Flac] = [],
            [Container.Ogg] = [],
            [Container.Mka] = [],
            [Container.Mks] = [],
            [Container.AudioHlsTs] = [],
            [Container.AudioHlsFmp4] = [],
        };

        foreach (Container c in allContainers)
        foreach (VideoCodecType v in allCodecs)
            map[(c, v)] = truth[c].Contains(v);

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
            [Container.Mks] = [],
            [Container.AudioHlsTs] = [AudioCodecType.Aac, AudioCodecType.Mp3],
            [Container.AudioHlsFmp4] = [AudioCodecType.Aac, AudioCodecType.Eac3],
            [Container.Dash] = [AudioCodecType.Aac, AudioCodecType.Eac3, AudioCodecType.Opus],
        };

        foreach (Container c in allContainers)
        foreach (AudioCodecType a in allCodecs)
            map[(c, a)] = truth[c].Contains(a);

        return map;
    }
}
