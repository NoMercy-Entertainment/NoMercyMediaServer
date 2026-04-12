namespace NoMercy.Tests.Encoder.V3.Pipeline.Optimizer;

using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline.Optimizer;
using NoMercy.Encoder.V3.Profiles;

public class ExecutionGraphBuilderTests
{
    private static readonly ExecutionGraphBuilder Builder = new();

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static VideoStreamInfo Sdr1080p =>
        new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );

    private static VideoStreamInfo Hdr4K =>
        new(
            Index: 0,
            Codec: "hevc",
            Width: 3840,
            Height: 2160,
            FrameRate: 24,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: "bt2020",
            ColorTransfer: "smpte2084",
            ColorSpace: "bt2020nc",
            IsDefault: true,
            BitRateKbps: 40000
        );

    private static AudioStreamInfo DefaultAudio =>
        new(
            Index: 1,
            Codec: "aac",
            Channels: 2,
            SampleRate: 48000,
            BitRateKbps: 192,
            Language: "eng",
            IsDefault: true,
            IsForced: false
        );

    private static SubtitleStreamInfo EnglishSub =>
        new(Index: 2, Codec: "subrip", Language: "eng", IsDefault: false, IsForced: false);

    private static SubtitleStreamInfo FrenchSub =>
        new(Index: 3, Codec: "subrip", Language: "fra", IsDefault: false, IsForced: false);

    private static ChapterInfo Chapter =>
        new(TimeSpan.Zero, TimeSpan.FromMinutes(90), "Main Feature");

    private static MediaInfo MakeMedia(
        IReadOnlyList<VideoStreamInfo>? video = null,
        IReadOnlyList<AudioStreamInfo>? audio = null,
        IReadOnlyList<SubtitleStreamInfo>? subs = null,
        IReadOnlyList<ChapterInfo>? chapters = null
    ) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 10000,
            FileSizeBytes: 8_000_000_000,
            VideoStreams: video ?? [],
            AudioStreams: audio ?? [DefaultAudio],
            SubtitleStreams: subs ?? [],
            Chapters: chapters ?? []
        );

    private static VideoOutput SingleOutput1080pH264 =>
        new(
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 23,
            Preset: "fast",
            Profile: null,
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

    private static EncodingProfile SingleOutputProfile =>
        new(
            Id: "test",
            Name: "Test",
            Format: OutputFormat.Hls,
            VideoOutputs: [SingleOutput1080pH264],
            AudioOutputs: [new AudioOutput(AudioCodecType.Aac, 192, 2, 48000, [])],
            SubtitleOutputs: []
        );

    private static EncoderInfo MakeEncoderInfo(string name, bool isHw) =>
        new(
            FfmpegName: name,
            RequiredVendor: isHw ? GpuVendor.Nvidia : null,
            Presets: ["fast", "medium", "slow"],
            Profiles: [],
            Levels: [],
            QualityRange: new QualityRange(0, 51, 23),
            SupportedRateControl: [RateControlMode.Crf],
            Supports10Bit: false,
            SupportsHdr: false,
            MaxConcurrentSessions: isHw ? 12 : int.MaxValue,
            PixelFormat10Bit: "yuv420p10le",
            VendorSpecificFlags: new Dictionary<string, string>()
        );

    private static ResolvedCodec H264Software =>
        new(
            FfmpegEncoderName: "libx264",
            EncoderInfo: MakeEncoderInfo("libx264", false),
            Device: null,
            DefaultRateControl: RateControlMode.Crf
        );

    private static ResolvedCodec H264Nvenc =>
        new(
            FfmpegEncoderName: "h264_nvenc",
            EncoderInfo: MakeEncoderInfo("h264_nvenc", true),
            Device: new GpuDevice(
                GpuVendor.Nvidia,
                "RTX 4090",
                24576,
                12,
                [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
            ),
            DefaultRateControl: RateControlMode.Cq
        );

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public void Simple1080pH264SingleOutput_HasDecodeAndEncodeNodes()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.Decode);
        nodes.Should().Contain(n => n.Operation == OperationType.Encode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Tonemap);
        nodes.Should().NotContain(n => n.Operation == OperationType.Split);
    }

    [Fact]
    public void Simple1080pH264SingleOutput_SameResolution_NoScaleNode()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.Scale);
    }

    [Fact]
    public void Simple1080pH264SingleOutput_EncodeNodeHasCorrectParameters()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], subs: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        ExecutionNode encode = nodes.Single(n => n.Operation == OperationType.Encode);
        encode.Parameters["encoder"].Should().Be("libx264");
        encode.Parameters["crf"].Should().Be("23");
        encode.Parameters["preset"].Should().Be("fast");
    }

    [Fact]
    public void Hdr4KMultiResolution_HasDecodeTonemapSplitScaleEncodeChain()
    {
        VideoOutput output1080p = new(
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 22,
            Preset: "medium",
            Profile: null,
            Level: null,
            ConvertHdrToSdr: true,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoOutput output720p = new(
            Codec: VideoCodecType.H265,
            Width: 1280,
            Height: 720,
            BitrateKbps: 2500,
            Crf: 22,
            Preset: "medium",
            Profile: null,
            Level: null,
            ConvertHdrToSdr: true,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoOutput output480p = new(
            Codec: VideoCodecType.H265,
            Width: 854,
            Height: 480,
            BitrateKbps: 1200,
            Crf: 22,
            Preset: "medium",
            Profile: null,
            Level: null,
            ConvertHdrToSdr: true,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );

        EncodingProfile profile = new(
            Id: "hdr-multi",
            Name: "HDR Multi",
            Format: OutputFormat.Hls,
            VideoOutputs: [output1080p, output720p, output480p],
            AudioOutputs: [new AudioOutput(AudioCodecType.Aac, 192, 2, 48000, [])],
            SubtitleOutputs: []
        );

        ResolvedCodec[] resolvedCodecs =
        [
            new ResolvedCodec(
                "hevc_nvenc",
                MakeEncoderInfo("hevc_nvenc", true),
                null,
                RateControlMode.Cq
            ),
            new ResolvedCodec(
                "hevc_nvenc",
                MakeEncoderInfo("hevc_nvenc", true),
                null,
                RateControlMode.Cq
            ),
            new ResolvedCodec(
                "hevc_nvenc",
                MakeEncoderInfo("hevc_nvenc", true),
                null,
                RateControlMode.Cq
            ),
        ];

        MediaInfo media = MakeMedia(video: [Hdr4K]);
        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, resolvedCodecs);

        nodes.Should().Contain(n => n.Operation == OperationType.Decode);
        nodes.Should().Contain(n => n.Operation == OperationType.Tonemap);
        nodes.Should().Contain(n => n.Operation == OperationType.Split);
        nodes.Count(n => n.Operation == OperationType.Scale).Should().Be(3);
        nodes.Count(n => n.Operation == OperationType.Encode).Should().Be(3);
    }

    [Fact]
    public void AudioOnlyInput_HasAudioDecodeAndEncodeOnly()
    {
        MediaInfo media = MakeMedia(video: [], audio: [DefaultAudio]);
        EncodingProfile profile = new(
            Id: "audio-only",
            Name: "Audio Only",
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [new AudioOutput(AudioCodecType.Aac, 192, 2, 48000, [])],
            SubtitleOutputs: []
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, []);

        nodes.Should().Contain(n => n.Operation == OperationType.AudioDecode);
        nodes.Should().Contain(n => n.Operation == OperationType.AudioEncode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Decode);
        nodes.Should().NotContain(n => n.Operation == OperationType.Encode);
    }

    [Fact]
    public void MultiSubtitleInput_SubtitleExtractNodesArePresentAndIndependentOfVideoChain()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], subs: [EnglishSub, FrenchSub]);
        SubtitleOutput subOutput = new(
            Codec: SubtitleCodecType.WebVtt,
            Mode: SubtitleMode.Extract,
            AllowedLanguages: []
        );
        EncodingProfile profile = new(
            Id: "subs",
            Name: "Subs",
            Format: OutputFormat.Hls,
            VideoOutputs: [SingleOutput1080pH264],
            AudioOutputs: [new AudioOutput(AudioCodecType.Aac, 192, 2, 48000, [])],
            SubtitleOutputs: [subOutput, subOutput]
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        IEnumerable<ExecutionNode> subNodes = nodes.Where(n =>
            n.Operation == OperationType.SubtitleExtract
        );
        subNodes.Should().HaveCount(2);

        // Subtitle nodes must have no dependencies on video nodes
        ExecutionNode decodeNode = nodes.Single(n => n.Operation == OperationType.Decode);
        foreach (ExecutionNode subNode in subNodes)
        {
            subNode.DependsOn.Should().NotContain(decodeNode.Id);
        }
    }

    [Fact]
    public void ProfileWithThumbnails_ThumbnailCaptureNodePresent()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p]);
        ThumbnailOutput thumbnails = new(Width: 320, IntervalSeconds: 10);
        EncodingProfile profile = new(
            Id: "thumbs",
            Name: "With Thumbs",
            Format: OutputFormat.Hls,
            VideoOutputs: [SingleOutput1080pH264],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: thumbnails
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        ExecutionNode thumbNode = nodes.Single(n => n.Operation == OperationType.ThumbnailCapture);
        thumbNode.Parameters["width"].Should().Be("320");
        thumbNode.Parameters["interval"].Should().Be("10");
    }

    [Fact]
    public void ProfileWithoutThumbnails_NoThumbnailCaptureNode()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p]);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.ThumbnailCapture);
    }

    [Fact]
    public void ChaptersPresent_ChapterExtractNodeAdded()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], chapters: [Chapter]);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.ChapterExtract);
    }

    [Fact]
    public void NoChapters_NoChapterExtractNode()
    {
        MediaInfo media = MakeMedia(video: [Sdr1080p], chapters: []);
        EncodingProfile profile = SingleOutputProfile;

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().NotContain(n => n.Operation == OperationType.ChapterExtract);
    }

    [Fact]
    public void AllNodes_HaveUniqueIds()
    {
        MediaInfo media = MakeMedia(
            video: [Sdr1080p],
            subs: [EnglishSub, FrenchSub],
            chapters: [Chapter]
        );
        SubtitleOutput subOutput = new(SubtitleCodecType.WebVtt, SubtitleMode.Extract, []);
        EncodingProfile profile = new(
            Id: "full",
            Name: "Full",
            Format: OutputFormat.Hls,
            VideoOutputs: [SingleOutput1080pH264],
            AudioOutputs: [new AudioOutput(AudioCodecType.Aac, 192, 2, 48000, [])],
            SubtitleOutputs: [subOutput, subOutput],
            Thumbnails: new ThumbnailOutput(320, 10)
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        IEnumerable<string> ids = nodes.Select(n => n.Id);
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SingleOutputSmallerResolution_ScaleNodeAdded()
    {
        // 4K source encoded to 1080p → needs Scale node
        VideoOutput output1080p = new(
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            BitrateKbps: 4000,
            Crf: 22,
            Preset: null,
            Profile: null,
            Level: null,
            ConvertHdrToSdr: false,
            KeyframeIntervalSeconds: 2,
            TenBit: false
        );
        VideoStreamInfo source4k = Hdr4K with
        {
            ColorPrimaries = "bt709",
            ColorTransfer = "bt709",
            ColorSpace = "bt709",
        };
        MediaInfo media = MakeMedia(video: [source4k]);
        EncodingProfile profile = new(
            Id: "scale-down",
            Name: "Scale Down",
            Format: OutputFormat.Hls,
            VideoOutputs: [output1080p],
            AudioOutputs: [],
            SubtitleOutputs: []
        );

        List<ExecutionNode> nodes = Builder.BuildGraph(media, profile, [H264Software]);

        nodes.Should().Contain(n => n.Operation == OperationType.Scale);
    }
}
