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
        string encoder = "libx264",
        string? cropFilter = null
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
            CropFilter: cropFilter
        );

    private static VideoOutputPlan TonemapVideo(
        int width = 1920,
        int height = 1080,
        string encoder = "libx264",
        string chain =
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable",
        string? cropFilter = null
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
            TonemapFilterChain: chain,
            CropFilter: cropFilter
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
            Thumbnails: hasThumbs ? new ThumbnailOutputPlan(160, 90, 10) : null
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
    public void GroupByDecodeClass_HdrPreserveAndSdrTonemap_AreTwoClassificationGroups()
    {
        // An HDR-preserve rung (real encoder, ConvertHdrToSdr=false) is a
        // plain Transcode, not Tonemap — Layer 1 classifies them into two
        // DecodeGroups because the tonemap CHAIN is meaningfully different
        // metadata. This does NOT mean they end up as two ffmpeg decodes:
        // Plan() (Layer 2) unions Transcode + Tonemap before capacity-
        // chunking because FilterGraphAssembler shares the crop + tonemap
        // dedupe across both in one ffmpeg — see the Plan_* co-bundling
        // tests below.
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
    public void Plan_HdrPreserveAndSdrTonemap_CoBundleIntoOneSharedDecode_SpriteRidesIt()
    {
        // A 4K HDR master rung (HDR-preserve, no tonemap) plus a 1080p SDR
        // rung derived from it (HDR->SDR tonemap) must land in ONE bundle —
        // FilterGraphAssembler hoists the source decode/crop once and dedupes
        // the tonemap into a single [sdr] intermediate that feeds the SDR
        // rung AND the sprite, so this is one ffmpeg, not two.
        DecomposedTask[] tasks =
        [
            VideoTask(0, "libx265", Cpu()), // 4K HDR-preserve master
            VideoTask(1, "libx264", Cpu()), // 1080p SDR tonemap, derived from the master
            AudioTask(0), // copy audio
            ThumbnailsTask(),
        ];
        OutputPlan plan = PlanWith(
            [TranscodeVideo(3840, 2160, "libx265"), TonemapVideo(1920, 1080, "libx264")],
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
            .ContainSingle(
                "the HDR master and the SDR rung derived from it share one decode+crop — "
                    + "total decodes = 1, not 2"
            );

        bundles[0]
            .VideoSliceIndexes.Should()
            .BeEquivalentTo(
                [0, 1],
                "both the HDR-preserve rung and the SDR-tonemap rung ride the single shared decode"
            );
        bundles[0]
            .IncludeThumbnails.Should()
            .NotBe(false, "the sprite must ride the shared decode for correct Rec.709 color");
        bundles[0].AudioSliceIndexes.Should().BeEquivalentTo([0]);
    }

    [Fact]
    public void Plan_CroppedHdrMasterPlusDerivedSdrRung_ShareOneBundle_NotReCroppedSeparately()
    {
        // Reproduces the reported regression: a letterboxed 2160p HDR source
        // (crop=3840:1608:0:276) with a "4K HDR" + "1080p SDR" preset ladder.
        // Both rungs resolve the SAME crop rectangle — the planner must put
        // them in one bundle so FilterGraphAssembler's single hoisted crop
        // feeds both, instead of the SDR rung re-cropping the original
        // source in its own standalone ffmpeg.
        const string crop = "3840:1608:0:276";
        DecomposedTask[] tasks =
        [
            VideoTask(0, "hevc_nvenc", Gpu()), // 4K HDR HEVC 10-bit master
            VideoTask(1, "hevc_nvenc", Gpu()), // 1080p SDR HEVC 10-bit, derived
        ];
        OutputPlan plan = PlanWith([
            TranscodeVideo(3840, 1608, "hevc_nvenc", cropFilter: crop),
            TonemapVideo(1920, 800, "hevc_nvenc", cropFilter: crop),
        ]);

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
                "the cropped HDR master and its derived SDR rung share one decode/crop bundle"
            );
        bundles[0]
            .VideoSliceIndexes.Should()
            .BeEquivalentTo(
                [0, 1],
                "the SDR rung is derived FROM the cropped HDR master, not re-cropped standalone"
            );
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
        DecomposedTask[] tasks = Enumerable
            .Range(0, 5)
            .Select(i => VideoTask(i, "libx264", Cpu()))
            .ToArray();
        OutputPlan plan = PlanWith(
            Enumerable.Range(0, 5).Select(i => TranscodeVideo(1920 - i * 100, 1080)).ToArray()
        );

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
