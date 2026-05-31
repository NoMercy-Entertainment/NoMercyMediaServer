using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Pipeline;

/// <summary>
/// PlanStageHelpers expands a profile's stream outputs — manual-ladder
/// rungs into per-rung VideoOutput entries, container → output-format
/// mapping. The expansion contract is what PlanStage relies on to emit a
/// stable rung-by-rung plan.
/// </summary>
public class PlanStageHelpersTests
{
    private static VideoOutput RefVideo() =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Vbr,
            Crf: 0,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.Main,
            Level: "4.0",
            Tune: null,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            KeyframeIntervalSeconds: 2,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: "video/{w}x{h}",
            PlaylistNameTemplate: "video/{w}x{h}/playlist"
        );

    private static EncodingProfile Profile(VideoOutput? video, LadderConfig? ladder) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "test",
            Container: Container.HlsFmp4,
            Video: video,
            Audio: [],
            Subtitles: [],
            Ladder: ladder
        );

    // ── EnumerateVideo ──────────────────────────────────────────────────────

    [Fact]
    public void EnumerateVideo_NoVideoNoLadder_ReturnsEmpty()
    {
        EncodingProfile profile = Profile(video: null, ladder: null);
        PlanStageHelpers.EnumerateVideo(profile).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateVideo_VideoNoLadder_ReturnsSingleEntry()
    {
        EncodingProfile profile = Profile(RefVideo(), ladder: null);
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile);

        result.Should().ContainSingle();
        result[0].Width.Should().Be(1920);
    }

    [Fact]
    public void EnumerateVideo_ManualLadder_MaterialisesEachRung()
    {
        EncodingProfile profile = Profile(
            RefVideo(),
            new()
            {
                Mode = LadderMode.Manual,
                Rungs =
                [
                    new(854, 480, VideoCodecType.H264, 800, 1000, 2000, 24),
                    new(1280, 720, VideoCodecType.H264, 2000, 2400, 4000, 24),
                    new(1920, 1080, VideoCodecType.H264, 4000, 4800, 8000, 24),
                ],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile);

        result.Should().HaveCount(3);
        result[0].Width.Should().Be(854);
        result[0].BitrateKbps.Should().Be(800);
        result[1].Width.Should().Be(1280);
        result[2].Width.Should().Be(1920);
    }

    [Fact]
    public void EnumerateVideo_RungInheritsReferenceFields()
    {
        EncodingProfile profile = Profile(
            RefVideo(),
            new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(1280, 720, VideoCodecType.H264, 2000, 0, 0, 24)],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile);

        // Preset / pixel format / keyframe interval / tune all come from
        // the reference video, not the rung.
        result[0].Preset.Should().Be("medium");
        result[0].PixelFormat.Should().Be("yuv420p");
        result[0].KeyframeIntervalSeconds.Should().Be(2);
    }

    [Fact]
    public void EnumerateVideo_AutoLadder_FallsBackToVideoSingle()
    {
        // AutoLadder is expanded by PlanStage via IAbrLadderGenerator BEFORE
        // this helper runs — so when EnumerateVideo sees Auto mode, it just
        // returns the single Video reference (or empty if null).
        EncodingProfile profile = Profile(RefVideo(), new() { Mode = LadderMode.Auto });
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile);

        result.Should().ContainSingle();
    }

    [Fact]
    public void EnumerateVideo_ManualLadderNoReferenceVideo_SynthesisesFromRung()
    {
        EncodingProfile profile = Profile(
            video: null,
            new()
            {
                Mode = LadderMode.Manual,
                Rungs = [new(1920, 1080, VideoCodecType.H265, 6000, 7200, 12000, 24)],
            }
        );
        VideoOutput[] result = PlanStageHelpers.EnumerateVideo(profile);

        result.Should().ContainSingle();
        result[0].Codec.Should().Be(VideoCodecType.H265);
        result[0].Width.Should().Be(1920);
        // Synthetic reference uses safe defaults — CRF mode + crf=23.
        result[0].Crf.Should().Be(23);
    }

    // ── ContainerToOutputFormat ─────────────────────────────────────────────

    [Theory]
    [InlineData(Container.HlsTs, OutputFormat.Hls)]
    [InlineData(Container.HlsFmp4, OutputFormat.Hls)]
    [InlineData(Container.AudioHlsTs, OutputFormat.AudioHls)]
    [InlineData(Container.AudioHlsFmp4, OutputFormat.AudioHls)]
    [InlineData(Container.Mkv, OutputFormat.Mkv)]
    [InlineData(Container.Mka, OutputFormat.Mkv)]
    [InlineData(Container.Mks, OutputFormat.Mkv)]
    [InlineData(Container.Mp4, OutputFormat.Mp4)]
    [InlineData(Container.Aac, OutputFormat.Mp4)]
    [InlineData(Container.Dash, OutputFormat.Dash)]
    [InlineData(Container.Mp3, OutputFormat.Mp3)]
    [InlineData(Container.Flac, OutputFormat.Flac)]
    [InlineData(Container.Ogg, OutputFormat.Ogg)]
    public void ContainerToOutputFormat_KnownContainers(Container c, OutputFormat expected)
    {
        PlanStageHelpers.ContainerToOutputFormat(c).Should().Be(expected);
    }
}
