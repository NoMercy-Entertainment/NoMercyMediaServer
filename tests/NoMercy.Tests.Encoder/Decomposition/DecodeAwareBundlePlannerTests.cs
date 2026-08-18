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
using NoMercy.Encoder.PostProcess;

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
        new(device, GpuSlots: 1, CpuThreads: 2);

    private static ResourceRequirement Cpu(int threads = 4) =>
        new(null, GpuSlots: 0, CpuThreads: threads);

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
            Thumbnails: hasThumbs
                ? new ThumbnailOutputPlan(160, 90, 10, SpriteGrid.For(TimeSpan.FromMinutes(24), 10))
                : null
        );

    // ------------------------------------------------------------------ Layer 1: GroupByDecodeClass

    [Fact]
    public void GroupByDecodeClass_CopyVideo_ClassifiedAsCopy()
    {
        DecomposedTask[] tasks = [VideoTask(0, "copy")];
        OutputPlan plan = PlanWith([CopyVideo()]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks,
            plan
        );

        groups.Should().ContainSingle();
        groups[0].Class.Should().Be(DecodeClass.Copy);
        groups[0].VideoTaskIndexes.Should().BeEquivalentTo([0]);
    }

    [Fact]
    public void GroupByDecodeClass_PlainTranscode_ClassifiedAsTranscode()
    {
        DecomposedTask[] tasks = [VideoTask(0, "libx264", Cpu())];
        OutputPlan plan = PlanWith([TranscodeVideo()]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks,
            plan
        );

        groups.Should().ContainSingle();
        groups[0].Class.Should().Be(DecodeClass.Transcode);
        groups[0].TonemapChain.Should().BeNull();
    }

    [Fact]
    public void GroupByDecodeClass_TonemapRung_ClassifiedAsTonemapWithChain()
    {
        const string chain = "zscale=t=linear,tonemap=hable";
        DecomposedTask[] tasks = [VideoTask(0, "libx264", Cpu())];
        OutputPlan plan = PlanWith([TonemapVideo(chain: chain)]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks,
            plan
        );

        groups.Should().ContainSingle();
        groups[0].Class.Should().Be(DecodeClass.Tonemap);
        groups[0].TonemapChain.Should().Be(chain);
    }

    [Fact]
    public void GroupByDecodeClass_HdrPreserveAndSdrTonemap_AreTwoSeparateGroups()
    {
        // An HDR-preserve rung (real encoder, ConvertHdrToSdr=false) is a
        // plain Transcode, not Tonemap — kept distinct even though
        // FilterGraphAssembler's dedupe path could technically combine it
        // with an SDR-tonemap rung into one ffmpeg via a pre-tonemap split.
        DecomposedTask[] tasks = [VideoTask(0, "libx265", Cpu()), VideoTask(1, "libx264", Cpu())];
        OutputPlan plan = PlanWith([
            TranscodeVideo(encoder: "libx265"),
            TonemapVideo(encoder: "libx264"),
        ]);

        IReadOnlyList<DecodeGroup> groups = DecodeAwareBundlePlanner.GroupByDecodeClass(
            tasks,
            plan
        );

        groups.Should().HaveCount(2);
        groups
            .Should()
            .ContainSingle(g =>
                g.Class == DecodeClass.Transcode && g.VideoTaskIndexes.SequenceEqual(new[] { 0 })
            );
        groups
            .Should()
            .ContainSingle(g =>
                g.Class == DecodeClass.Tonemap && g.VideoTaskIndexes.SequenceEqual(new[] { 1 })
            );
    }

    // ------------------------------------------------------------------ (a) HDR→SDR plan

    [Fact]
    public void Plan_HdrPreserveAndSdrTonemap_TonemapBundleCarriesSprite_HdrRungIsSeparateDecode()
    {
        DecomposedTask[] tasks =
        [
            VideoTask(0, "libx265", Cpu()), // HDR-preserve
            VideoTask(1, "libx264", Cpu()), // SDR tonemap
            AudioTask(0), // copy audio
            ThumbnailsTask(),
        ];
        OutputPlan plan = PlanWith(
            [TranscodeVideo(encoder: "libx265"), TonemapVideo(encoder: "libx264")],
            audios: [AudioOutput(StreamAction.Copy)],
            hasThumbs: true
        );

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .HaveCount(
                2,
                "the HDR-preserve rung and the SDR-tonemap rung are two separate decodes — "
                    + "total decodes = 2, not 3"
            );

        // Bundle 0 is the 4K HDR master: it runs FIRST so native-HDR clients can
        // play immediately, and it carries the audio so that rendition is
        // playable with sound the moment it lands.
        DecomposedTask hdrBundle = bundles[0];
        hdrBundle.VideoSliceIndexes.Should().BeEquivalentTo([0]);
        hdrBundle
            .IncludeThumbnails.Should()
            .Be(false, "the HDR master must not carry the sprite — it would sample raw HDR");
        hdrBundle
            .AudioSliceIndexes.Should()
            .BeEquivalentTo([0], "the 4K master carries the audio so it is playable first");

        // Bundle 1 is the SDR rung: it follows the master and carries the sprite,
        // which reuses its HDR→SDR tonemap for correct Rec.709 colour.
        DecomposedTask tonemapBundle = bundles[1];
        tonemapBundle.VideoSliceIndexes.Should().BeEquivalentTo([1]);
        tonemapBundle
            .IncludeThumbnails.Should()
            .NotBe(false, "the sprite rides the SDR-tonemap decode for correct Rec.709 color");
        tonemapBundle
            .AudioSliceIndexes.Should()
            .BeEmpty("copy audio rides the 4K master only, not the SDR bundle");
    }

    // ------------------------------------------------------------------ (b) All-copy plan

    [Fact]
    public void Plan_AllCopyPlan_RemuxBundleCarriesCopyVideo_SpriteIsOwnStandaloneBundle()
    {
        DecomposedTask[] tasks = [VideoTask(0, "copy"), AudioTask(0), ThumbnailsTask()];
        OutputPlan plan = PlanWith([CopyVideo()], audios: [AudioOutput()], hasThumbs: true);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .HaveCount(2, "one zero-decode remux bundle plus one standalone sprite bundle");

        DecomposedTask remux = bundles.Single(b => b.VideoSliceIndexes!.Length > 0);
        remux.VideoSliceIndexes.Should().BeEquivalentTo([0]);
        remux.AudioSliceIndexes.Should().BeEquivalentTo([0]);
        remux
            .IncludeThumbnails.Should()
            .BeFalse("the sprite never rides the zero-decode remux bundle");

        DecomposedTask sprite = bundles.Single(b => b != remux);
        sprite.VideoSliceIndexes.Should().BeEmpty();
        sprite.AudioSliceIndexes.Should().BeEmpty();
        sprite.IncludeThumbnails.Should().NotBe(false, "this is the single standalone sprite unit");
    }

    [Fact]
    public void Plan_NoVideoAtAll_ProducesOneAuxOnlyBundle()
    {
        // Edge case preserved from the pre-refactor bundler: an audio/
        // subtitle-only profile still needs one bundle so its streams run.
        DecomposedTask[] tasks = [AudioTask(0)];
        OutputPlan plan = PlanWith([], audios: [AudioOutput(StreamAction.Transcode)]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle();
        bundles[0].VideoSliceIndexes.Should().BeEmpty();
        bundles[0].AudioSliceIndexes.Should().BeEquivalentTo([0]);
    }

    [Fact]
    public void Plan_NoTasksAtAll_ProducesNoBundles()
    {
        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            [],
            PlanWith([]),
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
            VideoTask(0, "libx264", Cpu()),
            VideoTask(1, "libx264", Cpu()),
            VideoTask(2, "libx264", Cpu()),
            ThumbnailsTask(),
        ];
        OutputPlan plan = PlanWith(
            [TranscodeVideo(1920, 1080), TranscodeVideo(1280, 720), TranscodeVideo(854, 480)],
            hasThumbs: true
        );

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle("all three rungs share one decode — none of them tonemap");
        bundles[0].VideoSliceIndexes.Should().BeEquivalentTo([0, 1, 2]);
        bundles[0]
            .IncludeThumbnails.Should()
            .NotBe(false, "the sprite rides the one transcode decode");
    }

    [Fact]
    public void Plan_MixedGpuAndCpuRungsInSameClass_SplitIntoSeparateBundles()
    {
        // GPU and CPU rungs draw from different resource pools with
        // different caps — they are never packed into the same bundle even
        // when both fit comfortably under their own cap.
        DecomposedTask[] tasks =
        [
            VideoTask(0, "hevc_nvenc", Gpu()),
            VideoTask(1, "hevc_nvenc", Gpu()),
            VideoTask(2, "libx264", Cpu()),
        ];
        OutputPlan plan = PlanWith([
            TranscodeVideo(3840, 2160, "hevc_nvenc"),
            TranscodeVideo(1920, 1080, "hevc_nvenc"),
            TranscodeVideo(1920, 1080, "libx264"),
        ]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().HaveCount(2);
    }

    // ------------------------------------------------------------------ (d) Capacity overflow

    [Fact]
    public void Plan_CapacityOverflow_SplitsIntoFullDecodeBundlesOnDecodeIndependentBoundaries()
    {
        DecomposedTask[] tasks =
        [
            .. Enumerable.Range(0, 5).Select(i => VideoTask(i, "libx264", Cpu())),
        ];
        OutputPlan plan = PlanWith([
            .. Enumerable.Range(0, 5).Select(i => TranscodeVideo(1920 - i * 100, 1080)),
        ]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 2
        );

        bundles.Should().HaveCount(3, "ceil(5/2) = 3 capacity-bounded decode bundles");
        bundles
            .SelectMany(b => b.VideoSliceIndexes!)
            .Should()
            .BeEquivalentTo([0, 1, 2, 3, 4], "every rung still lands in exactly one bundle");
        bundles
            .Select(b => b.VideoSliceIndexes!.Length)
            .OrderDescending()
            .Should()
            .BeEquivalentTo([2, 2, 1]);
    }

    // ------------------------------------------------------------------ (e) Copy audio/subtitle never spawn their own decode

    [Fact]
    public void Plan_CopyAudioAndSubtitle_RideExistingDecodeGroup_NeverSpawnOwnDecode()
    {
        DecomposedTask[] tasks = [VideoTask(0, "libx264", Cpu()), AudioTask(0), SubtitleTask(0)];
        OutputPlan plan = PlanWith([TranscodeVideo()], audios: [AudioOutput(StreamAction.Copy)]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles
            .Should()
            .ContainSingle(
                "copy audio/subtitle ride the one real decode instead of spawning their own"
            );
        bundles[0].AudioSliceIndexes.Should().BeEquivalentTo([0]);
        bundles[0].SubtitleSliceIndexes.Should().BeEquivalentTo([0]);
    }

    [Fact]
    public void Plan_CopyVideoAlongsideTranscodeLadder_RidesTheDecodeGroupInsteadOfItsOwnBundle()
    {
        DecomposedTask[] tasks = [VideoTask(0, "libx264", Cpu()), VideoTask(1, "copy")];
        OutputPlan plan = PlanWith([TranscodeVideo(), CopyVideo()]);

        DecomposedTask[] bundles = DecodeAwareBundlePlanner.Plan(
            tasks,
            plan,
            parentJobId: 1,
            groupTag: GroupTag,
            gpuCap: 8,
            cpuCap: 8
        );

        bundles.Should().ContainSingle("copy video rides the transcode decode for free");
        bundles[0].VideoSliceIndexes.Should().BeEquivalentTo([0, 1]);
    }
}
