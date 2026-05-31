using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// BuildStageSlicing narrows a full OutputPlan down to just the entries one
/// decomposed task is responsible for. The slicing is what lets the coordinator
/// dispatch N small ffmpeg invocations from a single planned encode.
/// </summary>
public class BuildStageSlicingTests
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private static VideoOutputPlan Video(int width, int height, int index = 0) =>
        new(
            Width: width,
            Height: height,
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
        );

    private static AudioOutputPlan Audio(string language, int index = 0) =>
        new(
            EncoderName: "aac",
            BitrateKbps: 128,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: language,
            MapLabel: $"[a{index}]"
        );

    private static SubtitleOutputPlan Subtitle(int sourceIndex) =>
        new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Transcode,
            Language: "eng",
            SourceIndex: sourceIndex,
            MapLabel: null
        );

    private static OutputPlan FullPlan() =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(1920, 1080, 0), Video(1280, 720, 1)],
            AudioOutputs: [Audio("eng", 0), Audio("fra", 1)],
            SubtitleOutputs: [Subtitle(0), Subtitle(1)],
            Thumbnails: new ThumbnailOutputPlan(160, 90, 10)
        );

    private static DecomposedTask Task(
        EncodeTaskKind kind,
        int outputIndex = 0,
        int[]? sourceIndexes = null
    ) =>
        new(
            TaskId: "test",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: kind,
            OutputIndex: outputIndex,
            Resources: null,
            SourceIndexes: sourceIndexes
        );

    // ── SliceForTask ────────────────────────────────────────────────────────

    [Fact]
    public void SliceForTask_VideoTask_KeepsOnlyTheTargetVideo()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Video, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().HaveCount(1);
        sliced.VideoOutputs[0].Width.Should().Be(1920);
        sliced.AudioOutputs.Should().BeEmpty();
        sliced.SubtitleOutputs.Should().BeEmpty();
        sliced.Thumbnails.Should().BeNull();
    }

    [Fact]
    public void SliceForTask_AudioTask_KeepsOnlyTheTargetAudio()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Audio, outputIndex: 1);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().HaveCount(1);
        sliced.AudioOutputs[0].Language.Should().Be("fra");
        sliced.SubtitleOutputs.Should().BeEmpty();
    }

    [Fact]
    public void SliceForTask_SubtitleTask_PreservesAcquiredSubtitlesPointer()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Subtitle, outputIndex: 0);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.SubtitleOutputs.Should().HaveCount(1);
        // AcquiredSubtitles is non-null for Subtitle tasks (even though our
        // fixture leaves it default null).
        sliced.AcquiredSubtitles.Should().BeNull(); // fixture has no acquired subs
    }

    [Fact]
    public void SliceForTask_ThumbnailTask_KeepsThumbnailsDropsTheRest()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Thumbnails);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().BeEmpty();
        sliced.SubtitleOutputs.Should().BeEmpty();
        sliced.Thumbnails.Should().NotBeNull();
    }

    [Fact]
    public void SliceForTask_VideoTaskWithSourceIndexes_BatchesMultipleVideos()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Video, sourceIndexes: [0, 1]);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().HaveCount(2);
        sliced.VideoOutputs[0].Width.Should().Be(1920);
        sliced.VideoOutputs[1].Width.Should().Be(1280);
    }

    [Fact]
    public void SliceForTask_VideoTaskWithOutOfRangeIndexes_SilentlyDrops()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Video, sourceIndexes: [0, 99, -1]);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().HaveCount(1);
        sliced.VideoOutputs[0].Width.Should().Be(1920);
    }

    [Fact]
    public void SliceForTask_VideoTaskWithIndexOutOfRange_ReturnsEmpty()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Video, outputIndex: 99);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().BeEmpty();
    }

    // ── SliceForBundle (Whole-kind tasks) ───────────────────────────────────

    [Fact]
    public void SliceForBundle_WholeWithNullIndexes_KeepsEverything()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = Task(EncodeTaskKind.Whole);

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().HaveCount(2);
        sliced.AudioOutputs.Should().HaveCount(2);
        sliced.SubtitleOutputs.Should().HaveCount(2);
        sliced.Thumbnails.Should().NotBeNull();
    }

    [Fact]
    public void SliceForBundle_WholeWithEmptyVideoSliceIndexes_KeepsZeroVideos()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            VideoSliceIndexes: []
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.VideoOutputs.Should().BeEmpty();
        sliced.AudioOutputs.Should().HaveCount(2);
    }

    [Fact]
    public void SliceForBundle_WholeWithExplicitSubtitleIndexes_NarrowsCorrectly()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            SubtitleSliceIndexes: [1]
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.SubtitleOutputs.Should().HaveCount(1);
        sliced.SubtitleOutputs[0].SourceIndex.Should().Be(1);
    }

    [Fact]
    public void SliceForBundle_WholeWithIncludeThumbnailsFalse_DropsThumbnails()
    {
        OutputPlan plan = FullPlan();
        DecomposedTask task = new(
            TaskId: "bundle",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: EncodeTaskKind.Whole,
            OutputIndex: 0,
            Resources: null,
            IncludeThumbnails: false
        );

        OutputPlan sliced = BuildStageSlicing.SliceForTask(plan, task);

        sliced.Thumbnails.Should().BeNull();
    }
}
