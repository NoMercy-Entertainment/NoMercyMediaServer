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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mkv;
using NoMercy.Encoder.Strategies.Mp4;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies;

public class DecomposeTests
{
    private const string GroupTag = "01HZTEST00000000000000";

    // ------------------------------------------------------------------ helpers

    private static IEncoder MockEncoder()
    {
        Mock<IEncoder> mock = new();
        mock.Setup(expression: encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<NoMercy.Encoder.Progress.IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "test", GpuUsed: null)
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
            .Range(start: 0, count: videoCount)
            .Select(selector: index => new VideoOutputPlan(
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
        return BuildPlan(videos: videos, audioCount: audioCount, subtitleCount: subtitleCount, hasThumbnails: hasThumbnails);
    }

    private static OutputPlan MakeMixedCodecPlan()
    {
        // Smart-combine focus: HEVC ladder + H.264 1080p fallback should
        // collapse to TWO video tasks (one per encoder bucket), not 5.
        VideoOutputPlan[] videos =
        [
            MakeVideo(width: 3840, height: 2160, encoder: "hevc_nvenc", index: 0),
            MakeVideo(width: 1920, height: 1080, encoder: "hevc_nvenc", index: 1),
            MakeVideo(width: 1280, height: 720, encoder: "hevc_nvenc", index: 2),
            MakeVideo(width: 854, height: 480, encoder: "hevc_nvenc", index: 3),
            MakeVideo(width: 1920, height: 1080, encoder: "libx264", index: 4),
        ];
        return BuildPlan(videos: videos, audioCount: 0, subtitleCount: 0, hasThumbnails: false);
    }

    private static VideoOutputPlan MakeVideo(int width, int height, string encoder, int index) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoder,
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

    private static OutputPlan BuildPlan(
        VideoOutputPlan[] videos,
        int audioCount,
        int subtitleCount,
        bool hasThumbnails
    )
    {
        AudioOutputPlan[] audios = Enumerable
            .Range(start: 0, count: audioCount)
            .Select(selector: index => new AudioOutputPlan(
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
            .Range(start: 0, count: subtitleCount)
            .Select(selector: index => new SubtitleOutputPlan(
                OutputCodec: SubtitleCodecType.WebVtt,
                Action: StreamAction.Transcode,
                Language: "eng",
                SourceIndex: index,
                MapLabel: null
            ))
            .ToArray();

        ThumbnailOutputPlan? thumbnails = hasThumbnails
            ? new ThumbnailOutputPlan(Width: 160, Height: 68, IntervalSeconds: 10)
            : null;

        return new(
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
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan emptyPlan = MakePlan();

        DecomposedTask[] tasks = strategy.Decompose(plan: emptyPlan, groupTag: GroupTag);

        tasks.Should().HaveCount(expected: 1);
        tasks[0].Kind.Should().Be(expected: EncodeTaskKind.Whole);
        tasks[0].GroupTag.Should().Be(expected: GroupTag);
    }

    [Fact]
    public void HlsSinglePass_Decompose_PerStreamVideoTasks()
    {
        // Decompose emits one task per video rung — separation preserved
        // as the unit of tracking, retry, future distributed dispatch.
        // Dispatch-time bundling (in VideoEncodeJob.DispatchDecomposedAsync)
        // packs these into ONE ffmpeg invocation.
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 1);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask[] videoTasks = tasks
            .Where(predicate: task => task.Kind == EncodeTaskKind.Video)
            .ToArray();

        videoTasks.Should().HaveCount(expected: 2);
        videoTasks[0].OutputIndex.Should().Be(expected: 0);
        videoTasks[1].OutputIndex.Should().Be(expected: 1);

        tasks.Where(predicate: task => task.Kind == EncodeTaskKind.Audio).Should().HaveCount(expected: 1);
    }

    [Fact]
    public void HlsSinglePass_Decompose_MixedCodecs_OneTaskPerRung()
    {
        // HEVC ladder + H.264 fallback → one task per rung. The bundler
        // packs them into ONE ffmpeg at dispatch — each rung keeps its own
        // -map [vN] -c:v <encoder> block in the bundled invocation.
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakeMixedCodecPlan();

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask[] videoTasks = tasks
            .Where(predicate: task => task.Kind == EncodeTaskKind.Video)
            .ToArray();

        videoTasks.Should().HaveCount(expected: 5);
        videoTasks
            .Select(selector: task => task.OutputIndex)
            .Should()
            .BeEquivalentTo(expectation: new[] { 0, 1, 2, 3, 4 });
    }

    [Fact]
    public void HlsSinglePass_Decompose_PerTrackAudioTasks()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 1, audioCount: 3);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask[] audioTasks = tasks
            .Where(predicate: task => task.Kind == EncodeTaskKind.Audio)
            .ToArray();

        audioTasks.Should().HaveCount(expected: 3);
        audioTasks.Select(selector: task => task.OutputIndex).Should().BeEquivalentTo(expectation: new[] { 0, 1, 2 });
    }

    [Fact]
    public void HlsSinglePass_Decompose_WithThumbnails_IncludesThumbnailsTask()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 1, hasThumbnails: true);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask? thumbTask = tasks.FirstOrDefault(predicate: task =>
            task.Kind == EncodeTaskKind.Thumbnails
        );
        thumbTask.Should().NotBeNull();
        thumbTask!.OutputIndex.Should().Be(expected: 0);
    }

    [Fact]
    public void HlsSinglePass_Decompose_WithSubtitles_KeepsOneTaskPerSubtitle()
    {
        // Subtitles stay fanned out — each track is cheap and independent.
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 1, audioCount: 1, subtitleCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        DecomposedTask[] subTasks = tasks
            .Where(predicate: task => task.Kind == EncodeTaskKind.Subtitle)
            .ToArray();
        subTasks.Should().HaveCount(expected: 2);
        subTasks[0].OutputIndex.Should().Be(expected: 0);
        subTasks[1].OutputIndex.Should().Be(expected: 1);
    }

    [Fact]
    public void HlsSinglePass_Decompose_AllTasksShareGroupTag()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(
            videoCount: 2,
            audioCount: 1,
            subtitleCount: 1,
            hasThumbnails: true
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        tasks.Should().AllSatisfy(expected: task => task.GroupTag.Should().Be(expected: GroupTag));
    }

    // ------------------------------------------------------------------ DASH

    [Fact]
    public void DashSinglePass_Decompose_EmptyPlan_ReturnsSingleWhole()
    {
        DashSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<DashSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan emptyPlan = MakePlan();

        DecomposedTask[] tasks = strategy.Decompose(plan: emptyPlan, groupTag: GroupTag);

        tasks.Should().HaveCount(expected: 1);
        tasks[0].Kind.Should().Be(expected: EncodeTaskKind.Whole);
    }

    [Fact]
    public void DashSinglePass_Decompose_TwoVideoOneAudio_ReturnsCorrectCount()
    {
        DashSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<DashSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        tasks.Where(predicate: task => task.Kind == EncodeTaskKind.Video).Should().HaveCount(expected: 2);
        tasks.Where(predicate: task => task.Kind == EncodeTaskKind.Audio).Should().HaveCount(expected: 2);
    }

    // ------------------------------------------------------------------ MP4 / MKV (whole-task strategies)

    [Fact]
    public void Mp4SinglePass_Decompose_AlwaysReturnsWholeTask()
    {
        IEncodingStrategy strategy = new Mp4SinglePassStrategy(
            encoder: MockEncoder(),
            logger: NullLogger<Mp4SinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 3, audioCount: 2);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        tasks.Should().HaveCount(expected: 1);
        tasks[0].Kind.Should().Be(expected: EncodeTaskKind.Whole);
    }

    [Fact]
    public void MkvStrategy_Decompose_AlwaysReturnsWholeTask()
    {
        IEncodingStrategy strategy = new MkvStrategy(
            encoder: MockEncoder(),
            logger: NullLogger<MkvStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(videoCount: 2, audioCount: 1);

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        tasks.Should().HaveCount(expected: 1);
        tasks[0].Kind.Should().Be(expected: EncodeTaskKind.Whole);
    }

    // ------------------------------------------------------------------ TaskIds are unique

    [Fact]
    public void HlsSinglePass_Decompose_AllTaskIdsAreUnique()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: MockEncoder(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = MakePlan(
            videoCount: 3,
            audioCount: 2,
            subtitleCount: 1,
            hasThumbnails: true
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: GroupTag);

        string[] taskIds = tasks.Select(selector: task => task.TaskId).ToArray();
        taskIds.Should().OnlyHaveUniqueItems(because: "task IDs must be unique within a decomposition run");
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

        string json = JsonConvert.SerializeObject(value: original);
        DecomposedTask? deserialized = JsonConvert.DeserializeObject<DecomposedTask>(value: json);

        deserialized.Should().NotBeNull();
        deserialized!.TaskId.Should().Be(expected: original.TaskId);
        deserialized.ParentJobId.Should().Be(expected: original.ParentJobId);
        deserialized.GroupTag.Should().Be(expected: original.GroupTag);
        deserialized.Kind.Should().Be(expected: original.Kind);
        deserialized.OutputIndex.Should().Be(expected: original.OutputIndex);
        deserialized.EstimatedCostUnits.Should().Be(expected: original.EstimatedCostUnits);
        deserialized.Label.Should().Be(expected: original.Label);
    }

    [Fact]
    public void DecomposedTask_DependencyFields_DefaultNullAndRoundTrip()
    {
        DecomposedTask noDep = new(
            TaskId: "tag-video-0",
            ParentJobId: 42,
            GroupTag: "tag",
            Kind: EncodeTaskKind.Video,
            OutputIndex: 0,
            Resources: null
        );

        noDep.DependsOnTaskId.Should().BeNull(because: "default = reads source, exactly as before");
        noDep.InputArtifactKey.Should().BeNull();

        DecomposedTask derived = noDep with
        {
            DependsOnTaskId = "tag-mezzanine",
            InputArtifactKey = "mezzanine/sdr_fullres.mkv",
        };

        DecomposedTask? round = JsonConvert.DeserializeObject<DecomposedTask>(
            value: JsonConvert.SerializeObject(value: derived)
        );

        round.Should().NotBeNull();
        round!.DependsOnTaskId.Should().Be(expected: "tag-mezzanine");
        round.InputArtifactKey.Should().Be(expected: "mezzanine/sdr_fullres.mkv");
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

            string json = JsonConvert.SerializeObject(value: task);
            DecomposedTask? restored = JsonConvert.DeserializeObject<DecomposedTask>(value: json);

            restored!.Kind.Should().Be(expected: kind, because: $"Kind.{kind} should survive JSON round-trip");
        }
    }
}
