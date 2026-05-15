using Moq;
using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mkv;
using NoMercy.Encoder.Strategies.Mp4;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies;

public class DecomposeTests
{
    private const string GroupTag = "01HZTEST00000000000000";

    // ------------------------------------------------------------------ helpers

    private static IEncoder MockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );
        return mock.Object;
    }

    private static OutputPlan MakePlan(
        int videoCount = 0,
        int audioCount = 0,
        int subtitleCount = 0,
        bool hasThumbnails = false
    )
    {
        VideoOutputPlan[] videos = Enumerable
            .Range(0, videoCount)
            .Select(index => new VideoOutputPlan(
                Width: index == 0 ? 1920 : 1280,
                Height: index == 0 ? 1080 : 720,
                EncoderName: "libx264",
                Crf: 23,
                BitrateKbps: 0,
                Preset: "medium",
                Profile: "main",
                Level: "4.0",
                TenBit: false,
                PixelFormat: "yuv420p",
                MapLabel: $"[v{index}]",
                ExtraFlags: []
            ))
            .ToArray();

        AudioOutputPlan[] audios = Enumerable
            .Range(0, audioCount)
            .Select(index => new AudioOutputPlan(
                EncoderName: "aac",
                BitrateKbps: 128,
                Channels: 2,
                SampleRate: 48000,
                Action: StreamAction.Transcode,
                Language: index == 0 ? "eng" : "fra",
                MapLabel: $"[a{index}]"
            ))
            .ToArray();

        SubtitleOutputPlan[] subtitles = Enumerable
            .Range(0, subtitleCount)
            .Select(index => new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Transcode,
                Language: "eng",
                SourceIndex: index,
                MapLabel: null
            ))
            .ToArray();

        ThumbnailOutputPlan? thumbnails = hasThumbnails
            ? new ThumbnailOutputPlan(160, 68, 10)
            : null;

        return new OutputPlan(
            Format: OutputFormat.Hls,
            VideoOutputs: videos,
            AudioOutputs: audios,
            SubtitleOutputs: subtitles,
            Thumbnails: thumbnails
        );
    }

    // ------------------------------------------------------------------ HLS

    [Fact]
    public void HlsSinglePass_Decompose_EmptyPlan_ReturnsSingleWhole()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan emptyPlan = MakePlan();

        DecomposedTask[] tasks = strategy.Decompose(emptyPlan, GroupTag);

        tasks.Should().HaveCount(1);
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
        tasks[0].GroupTag.Should().Be(GroupTag);
    }

    [Fact]
    public void HlsSinglePass_Decompose_TwoVideoOneAudio_ReturnsSeparateTasks()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 1);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Should().HaveCount(3);

        DecomposedTask[] videoTasks = tasks
            .Where(task => task.Kind == EncodeTaskKind.Video)
            .ToArray();
        DecomposedTask[] audioTasks = tasks
            .Where(task => task.Kind == EncodeTaskKind.Audio)
            .ToArray();

        videoTasks.Should().HaveCount(2);
        audioTasks.Should().HaveCount(1);

        videoTasks[0].OutputIndex.Should().Be(0);
        videoTasks[1].OutputIndex.Should().Be(1);
        audioTasks[0].OutputIndex.Should().Be(0);
    }

    [Fact]
    public void HlsSinglePass_Decompose_WithThumbnails_IncludesThumbnailsTask()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 1, hasThumbnails: true);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        DecomposedTask? thumbTask = tasks.FirstOrDefault(task =>
            task.Kind == EncodeTaskKind.Thumbnails
        );
        thumbTask.Should().NotBeNull();
        thumbTask!.OutputIndex.Should().Be(0);
    }

    [Fact]
    public void HlsSinglePass_Decompose_WithSubtitles_IncludesSubtitleTasks()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 1, audioCount: 1, subtitleCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        DecomposedTask[] subTasks = tasks
            .Where(task => task.Kind == EncodeTaskKind.Subtitle)
            .ToArray();
        subTasks.Should().HaveCount(2);
        subTasks[0].OutputIndex.Should().Be(0);
        subTasks[1].OutputIndex.Should().Be(1);
    }

    [Fact]
    public void HlsSinglePass_Decompose_AllTasksShareGroupTag()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(
            videoCount: 2,
            audioCount: 1,
            subtitleCount: 1,
            hasThumbnails: true
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Should().AllSatisfy(task => task.GroupTag.Should().Be(GroupTag));
    }

    [Fact]
    public void HlsSinglePass_Decompose_HighRes_HigherCostUnit()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 2); // [0] = 1920p (cost=4), [1] = 1280p (cost=2)

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        DecomposedTask[] videoTasks = tasks
            .Where(task => task.Kind == EncodeTaskKind.Video)
            .ToArray();
        videoTasks[0].EstimatedCostUnits.Should().Be(4);
        videoTasks[1].EstimatedCostUnits.Should().Be(2);
    }

    // ------------------------------------------------------------------ DASH

    [Fact]
    public void DashSinglePass_Decompose_EmptyPlan_ReturnsSingleWhole()
    {
        DashSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan emptyPlan = MakePlan();

        DecomposedTask[] tasks = strategy.Decompose(emptyPlan, GroupTag);

        tasks.Should().HaveCount(1);
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
    }

    [Fact]
    public void DashSinglePass_Decompose_TwoVideoOneAudio_ReturnsCorrectCount()
    {
        DashSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Where(task => task.Kind == EncodeTaskKind.Video).Should().HaveCount(2);
        tasks.Where(task => task.Kind == EncodeTaskKind.Audio).Should().HaveCount(2);
    }

    // ------------------------------------------------------------------ MP4 / MKV (whole-task strategies)

    [Fact]
    public void Mp4SinglePass_Decompose_AlwaysReturnsWholeTask()
    {
        IEncodingStrategy strategy = new Mp4SinglePassStrategy(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 3, audioCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Should().HaveCount(1);
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
    }

    [Fact]
    public void MkvStrategy_Decompose_AlwaysReturnsWholeTask()
    {
        IEncodingStrategy strategy = new MkvStrategy(MockEncoder());
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 1);

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        tasks.Should().HaveCount(1);
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
    }

    // ------------------------------------------------------------------ TaskIds are unique

    [Fact]
    public void HlsSinglePass_Decompose_AllTaskIdsAreUnique()
    {
        HlsSinglePassStrategy strategy = new(MockEncoder());
        OutputPlan plan = MakePlan(
            videoCount: 3,
            audioCount: 2,
            subtitleCount: 1,
            hasThumbnails: true
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, GroupTag);

        string[] taskIds = tasks.Select(task => task.TaskId).ToArray();
        taskIds.Should().OnlyHaveUniqueItems("task IDs must be unique within a decomposition run");
    }

    // ------------------------------------------------------------------ Payload round-trip

    [Fact]
    public void DecomposedTask_RoundTrips_ViaNewtonsoftJson()
    {
        DecomposedTask original = new(
            TaskId: "tag-video-0",
            ParentJobId: 42,
            GroupTag: "tag",
            Kind: EncodeTaskKind.Video,
            OutputIndex: 0,
            Resources: null,
            EstimatedCostUnits: 4,
            Label: "1080p libx264"
        );

        string json = JsonConvert.SerializeObject(original);
        DecomposedTask? deserialized = JsonConvert.DeserializeObject<DecomposedTask>(json);

        deserialized.Should().NotBeNull();
        deserialized!.TaskId.Should().Be(original.TaskId);
        deserialized.ParentJobId.Should().Be(original.ParentJobId);
        deserialized.GroupTag.Should().Be(original.GroupTag);
        deserialized.Kind.Should().Be(original.Kind);
        deserialized.OutputIndex.Should().Be(original.OutputIndex);
        deserialized.EstimatedCostUnits.Should().Be(original.EstimatedCostUnits);
        deserialized.Label.Should().Be(original.Label);
    }

    [Fact]
    public void DecomposedTask_AllKinds_RoundTripCorrectly()
    {
        foreach (EncodeTaskKind kind in Enum.GetValues<EncodeTaskKind>())
        {
            DecomposedTask task = new(
                TaskId: $"tag-{kind.ToString().ToLowerInvariant()}-0",
                ParentJobId: 1,
                GroupTag: GroupTag,
                Kind: kind,
                OutputIndex: 0,
                Resources: null
            );

            string json = JsonConvert.SerializeObject(task);
            DecomposedTask? restored = JsonConvert.DeserializeObject<DecomposedTask>(json);

            restored!.Kind.Should().Be(kind, $"Kind.{kind} should survive JSON round-trip");
        }
    }
}
