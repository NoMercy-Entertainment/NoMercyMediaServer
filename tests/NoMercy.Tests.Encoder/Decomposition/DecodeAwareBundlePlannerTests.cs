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
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Decomposition;

/// <summary>
/// A full ffmpeg decode is the real cost of an encode, not the rung count.
/// These tests assert the actual decode-class grouping (Layer 1) and
/// capacity-bounded bundle split (Layer 2) <see cref="DecodeAwareBundlePlanner"/>
/// produces — never mere existence of a result.
/// </summary>
public class DecodeAwareBundlePlannerTests
{
    private const string GroupTag = "01HZTEST00000000000000";

    // ------------------------------------------------------------------ helpers

    private static DecomposedTask VideoTask(
        int outputIndex,
        string encoderName,
        ResourceRequirement? resources = null
    ) =>
        new(
            TaskId: $"{GroupTag}-video-{outputIndex}",
            ParentJobId: 0,
            GroupTag: GroupTag,
            Kind: EncodeTaskKind.Video,
            OutputIndex: outputIndex,
            Resources: resources,
            VideoWidth: 1920,
            VideoEncoderName: encoderName
        );

    private static DecomposedTask AudioTask(int outputIndex = 0) =>
        new(
            TaskId: $"{GroupTag}-audio-{outputIndex}",
            ParentJobId: 0,
            GroupTag: GroupTag,
            Kind: EncodeTaskKind.Audio,
            OutputIndex: outputIndex,
            Resources: null
        );

    private static DecomposedTask SubtitleTask(int outputIndex = 0) =>
        new(
            TaskId: $"{GroupTag}-sub-{outputIndex}",
            ParentJobId: 0,
            GroupTag: GroupTag,
            Kind: EncodeTaskKind.Subtitle,
            OutputIndex: outputIndex,
            Resources: null
        );

    private static DecomposedTask ThumbnailsTask() =>
        new(
            TaskId: $"{GroupTag}-thumbs",
            ParentJobId: 0,
            GroupTag: GroupTag,
            Kind: EncodeTaskKind.Thumbnails,
            OutputIndex: 0,
            Resources: null
        );

    private static ResourceRequirement Gpu(string device = "gpu0") =>
        new(GpuDeviceKey: device, GpuSlots: 1, CpuThreads: 2);

    private static ResourceRequirement Cpu(int threads = 4) =>
        new(GpuDeviceKey: null, GpuSlots: 0, CpuThreads: threads);

    private static VideoOutputPlan CopyVideo(int index = 0) =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "copy",
            Crf: 0,
            BitrateKbps: 0,
            Preset: null,
            Profile: null,
            Level: null,
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "0:v:0",
            ExtraFlags: []
        );

    private static VideoOutputPlan TranscodeVideo(
        int width = 1920,
        int height = 1080,
        string encoder = "libx264"
    ) =>
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
            MapLabel: "[v]",
            ExtraFlags: []
        );

    private static VideoOutputPlan TonemapVideo(
        int width = 1920,
        int height = 1080,
        string encoder = "libx264",
        string chain =
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable"
    ) =>
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
            MapLabel: "[v]",
            ExtraFlags: [],
            ConvertHdrToSdr: true,
            TonemapFilterChain: chain
        );

    private static AudioOutputPlan AudioOutput(StreamAction action = StreamAction.Copy) =>
        new(
            EncoderName: action == StreamAction.Copy ? "copy" : "aac",
            BitrateKbps: 128,
            Channels: 2,
            SampleRate: 48000,
            Action: action,
            Language: "eng",
            MapLabel: "[a0]"
        );

    private static OutputPlan PlanWith(
        VideoOutputPlan[] videos,
        AudioOutputPlan[]? audios = null,
        bool hasThumbs = false
    ) =>
        new(
            Format: OutputFormat.Hls,
            VideoOutputs: videos,
            AudioOutputs: audios ?? [],
            SubtitleOutputs: [],
            Thumbnails: hasThumbs ? new ThumbnailOutputPlan(Width: 160, Height: 90, IntervalSeconds: 10) : null
        );

    // ------------------------------------------------------------------ Layer 1: GroupByDecodeClass

    [Fact]
    public void GroupByDecodeClass_CopyVideo_ClassifiedAsCopy()
    {
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "copy")];
        OutputPlan plan = PlanWith(videos: [CopyVideo()]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks: tasks,
            plan: plan
        );

        groups.Should().ContainSingle();
        groups[index: 0].Class.Should().Be(expected: DecodeClass.Copy);
        groups[index: 0].VideoTaskIndexes.Should().BeEquivalentTo(expectation: [0]);
    }

    [Fact]
    public void GroupByDecodeClass_PlainTranscode_ClassifiedAsTranscode()
    {
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "libx264", resources: Cpu())];
        OutputPlan plan = PlanWith(videos: [TranscodeVideo()]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks: tasks,
            plan: plan
        );

        groups.Should().ContainSingle();
        groups[index: 0].Class.Should().Be(expected: DecodeClass.Transcode);
        groups[index: 0].TonemapChain.Should().BeNull();
    }

    [Fact]
    public void GroupByDecodeClass_TonemapRung_ClassifiedAsTonemapWithChain()
    {
        const string chain = "zscale=t=linear,tonemap=hable";
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "libx264", resources: Cpu())];
        OutputPlan plan = PlanWith(videos: [TonemapVideo(chain: chain)]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks: tasks,
            plan: plan
        );

        groups.Should().ContainSingle();
        groups[index: 0].Class.Should().Be(expected: DecodeClass.Tonemap);
        groups[index: 0].TonemapChain.Should().Be(expected: chain);
    }

    [Fact]
    public void GroupByDecodeClass_HdrPreserveAndSdrTonemap_AreTwoSeparateGroups()
    {
        // An HDR-preserve rung (real encoder, ConvertHdrToSdr=false) is a
        // plain Transcode, not Tonemap — kept distinct even though
        // FilterGraphAssembler's dedupe path could technically combine it
        // with an SDR-tonemap rung into one ffmpeg via a pre-tonemap split.
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "libx265", resources: Cpu()), VideoTask(outputIndex: 1, encoderName: "libx264", resources: Cpu())];
        OutputPlan plan = PlanWith(videos:
        [
            TranscodeVideo(encoder: "libx265"),
            TonemapVideo(encoder: "libx264"),
        ]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks: tasks,
            plan: plan
        );

        groups.Should().HaveCount(expected: 2);
        groups
            .Should()
            .ContainSingle(predicate: g =>
                g.Class == DecodeClass.Transcode && g.VideoTaskIndexes.SequenceEqual(new[] { 0 })
            );
        groups
            .Should()
            .ContainSingle(predicate: g =>
                g.Class == DecodeClass.Tonemap && g.VideoTaskIndexes.SequenceEqual(new[] { 1 })
            );
    }

    // ------------------------------------------------------------------ (a) HDR→SDR plan

    [Fact]
    public void Plan_HdrPreserveAndSdrTonemap_TonemapBundleCarriesSprite_HdrRungIsSeparateDecode()
    {
        DecomposedTask[] tasks =
        [
            VideoTask(outputIndex: 0, encoderName: "libx265", resources: Cpu()), // HDR-preserve
            VideoTask(outputIndex: 1, encoderName: "libx264", resources: Cpu()), // SDR tonemap
            AudioTask(outputIndex: 0), // copy audio
            ThumbnailsTask(),
        ];
        OutputPlan plan = PlanWith(
            videos: [TranscodeVideo(encoder: "libx265"), TonemapVideo(encoder: "libx264")],
            audios: [AudioOutput(action: StreamAction.Copy)],
            hasThumbs: true
        );

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .HaveCount(
                expected: 2,
                because: "the HDR-preserve rung and the SDR-tonemap rung are two separate decodes — "
                         + "total decodes = 2, not 3"
            );

        // Bundle 0 is the 4K HDR master: it runs FIRST so native-HDR clients can
        // play immediately, and it carries the audio so that rendition is
        // playable with sound the moment it lands.
        DecomposedTask hdrBundle = bundles[0];
        hdrBundle.VideoSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
        hdrBundle
            .IncludeThumbnails.Should()
            .Be(expected: false, because: "the HDR master must not carry the sprite — it would sample raw HDR");
        hdrBundle
            .AudioSliceIndexes.Should()
            .BeEquivalentTo(expectation: [0], because: "the 4K master carries the audio so it is playable first");

        // Bundle 1 is the SDR rung: it follows the master and carries the sprite,
        // which reuses its HDR→SDR tonemap for correct Rec.709 colour.
        DecomposedTask tonemapBundle = bundles[1];
        tonemapBundle.VideoSliceIndexes.Should().BeEquivalentTo(expectation: [1]);
        tonemapBundle
            .IncludeThumbnails.Should()
            .NotBe(unexpected: false, because: "the sprite rides the SDR-tonemap decode for correct Rec.709 color");
        tonemapBundle
            .AudioSliceIndexes.Should()
            .BeEmpty(because: "copy audio rides the 4K master only, not the SDR bundle");
    }

    // ------------------------------------------------------------------ (b) All-copy plan

    [Fact]
    public void Plan_AllCopyPlan_RemuxBundleCarriesCopyVideo_SpriteIsOwnStandaloneBundle()
    {
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "copy"), AudioTask(outputIndex: 0), ThumbnailsTask()];
        OutputPlan plan = PlanWith(videos: [CopyVideo()], audios: [AudioOutput()], hasThumbs: true);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .HaveCount(expected: 2, because: "one zero-decode remux bundle plus one standalone sprite bundle");

        DecomposedTask remux = bundles.Single(predicate: b => b.VideoSliceIndexes!.Length > 0);
        remux.VideoSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
        remux.AudioSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
        remux
            .IncludeThumbnails.Should()
            .BeFalse(because: "the sprite never rides the zero-decode remux bundle");

        DecomposedTask sprite = bundles.Single(predicate: b => b != remux);
        sprite.VideoSliceIndexes.Should().BeEmpty();
        sprite.AudioSliceIndexes.Should().BeEmpty();
        sprite.IncludeThumbnails.Should().NotBe(unexpected: false, because: "this is the single standalone sprite unit");
    }

    [Fact]
    public void Plan_NoVideoAtAll_ProducesOneAuxOnlyBundle()
    {
        // Edge case preserved from the pre-refactor bundler: an audio/
        // subtitle-only profile still needs one bundle so its streams run.
        DecomposedTask[] tasks = [AudioTask(outputIndex: 0)];
        OutputPlan plan = PlanWith(videos: [], audios: [AudioOutput(action: StreamAction.Transcode)]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle();
        bundles[0].VideoSliceIndexes.Should().BeEmpty();
        bundles[0].AudioSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
    }

    [Fact]
    public void Plan_NoTasksAtAll_ProducesNoBundles()
    {
        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: [],
            plan: PlanWith(videos: []),
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ (c) Pure-SDR transcode ladder

    [Fact]
    public void Plan_PureSdrTranscodeLadder_UnderCap_OneDecodeCarriesEverything()
    {
        DecomposedTask[] tasks =
        [
            VideoTask(outputIndex: 0, encoderName: "libx264", resources: Cpu()),
            VideoTask(outputIndex: 1, encoderName: "libx264", resources: Cpu()),
            VideoTask(outputIndex: 2, encoderName: "libx264", resources: Cpu()),
            ThumbnailsTask(),
        ];
        OutputPlan plan = PlanWith(
            videos: [TranscodeVideo(width: 1920, height: 1080), TranscodeVideo(width: 1280, height: 720), TranscodeVideo(width: 854, height: 480)],
            hasThumbs: true
        );

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle(because: "all three rungs share one decode — none of them tonemap");
        bundles[0].VideoSliceIndexes.Should().BeEquivalentTo(expectation: [0, 1, 2]);
        bundles[0]
            .IncludeThumbnails.Should()
            .NotBe(unexpected: false, because: "the sprite rides the one transcode decode");
    }

    [Fact]
    public void Plan_MixedGpuAndCpuRungsInSameClass_SplitIntoSeparateBundles()
    {
        // GPU and CPU rungs draw from different resource pools with
        // different caps — they are never packed into the same bundle even
        // when both fit comfortably under their own cap.
        DecomposedTask[] tasks =
        [
            VideoTask(outputIndex: 0, encoderName: "hevc_nvenc", resources: Gpu()),
            VideoTask(outputIndex: 1, encoderName: "hevc_nvenc", resources: Gpu()),
            VideoTask(outputIndex: 2, encoderName: "libx264", resources: Cpu()),
        ];
        OutputPlan plan = PlanWith(videos:
        [
            TranscodeVideo(width: 3840, height: 2160, encoder: "hevc_nvenc"),
            TranscodeVideo(width: 1920, height: 1080, encoder: "hevc_nvenc"),
            TranscodeVideo(width: 1920, height: 1080, encoder: "libx264"),
        ]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().HaveCount(expected: 2);
    }

    // ------------------------------------------------------------------ (d) Capacity overflow

    [Fact]
    public void Plan_CapacityOverflow_SplitsIntoFullDecodeBundlesOnDecodeIndependentBoundaries()
    {
        DecomposedTask[] tasks = Enumerable
            .Range(start: 0, count: 5)
            .Select(selector: i => VideoTask(outputIndex: i, encoderName: "libx264", resources: Cpu()))
            .ToArray();
        OutputPlan plan = PlanWith(
            videos: Enumerable.Range(start: 0, count: 5).Select(selector: i => TranscodeVideo(width: 1920 - i * 100, height: 1080)).ToArray()
        );

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 2
        );

        bundles.Should().HaveCount(expected: 3, because: "ceil(5/2) = 3 capacity-bounded decode bundles");
        bundles
            .SelectMany(selector: b => b.VideoSliceIndexes!)
            .Should()
            .BeEquivalentTo(expectation: [0, 1, 2, 3, 4], because: "every rung still lands in exactly one bundle");
        bundles
            .Select(selector: b => b.VideoSliceIndexes!.Length)
            .OrderDescending()
            .Should()
            .BeEquivalentTo(expectation: [2, 2, 1]);
    }

    // ------------------------------------------------------------------ (e) Copy audio/subtitle never spawn their own decode

    [Fact]
    public void Plan_CopyAudioAndSubtitle_RideExistingDecodeGroup_NeverSpawnOwnDecode()
    {
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "libx264", resources: Cpu()), AudioTask(outputIndex: 0), SubtitleTask(outputIndex: 0)];
        OutputPlan plan = PlanWith(videos: [TranscodeVideo()], audios: [AudioOutput(action: StreamAction.Copy)]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .ContainSingle(
                because: "copy audio/subtitle ride the one real decode instead of spawning their own"
            );
        bundles[0].AudioSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
        bundles[0].SubtitleSliceIndexes.Should().BeEquivalentTo(expectation: [0]);
    }

    [Fact]
    public void Plan_CopyVideoAlongsideTranscodeLadder_RidesTheDecodeGroupInsteadOfItsOwnBundle()
    {
        DecomposedTask[] tasks = [VideoTask(outputIndex: 0, encoderName: "libx264", resources: Cpu()), VideoTask(outputIndex: 1, encoderName: "copy")];
        OutputPlan plan = PlanWith(videos: [TranscodeVideo(), CopyVideo()]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle(because: "copy video rides the transcode decode for free");
        bundles[0].VideoSliceIndexes.Should().BeEquivalentTo(expectation: [0, 1]);
    }
}
