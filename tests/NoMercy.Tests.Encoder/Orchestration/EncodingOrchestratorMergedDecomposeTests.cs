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
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Strategies;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Orchestration;

/// <summary>
/// The smart-orchestrator "unify multiple presets into one coordinated
/// encode" seam: <see cref="EncodingOrchestrator.DecomposeMergedAsync"/> and
/// <see cref="EncodingOrchestrator.PlanMergedAsync"/>. Covers the three
/// acceptance criteria from the slice spec:
///   (a) merging two profiles yields one plan with both video renditions —
///       proven at the <c>OutputPlanMerger</c> layer (see
///       <c>OutputPlanMergerTests</c>); this file proves the orchestrator
///       wires that merge into a single <c>strategy.Decompose</c> call.
///   (b) collision guard — covered by <c>OutputPlanMergerTests</c>.
///   (c) a single preset merges to an identical plan as before — proven here
///       by asserting <c>DecomposeMergedAsync</c> with one request produces
///       the exact same tasks as <c>DecomposeAsync</c>.
/// Plus the fallback contract: incompatible presets (different container
/// formats, different encode modes, a plan failure) throw
/// <see cref="MergedEncodingIncompatibleException"/> rather than silently
/// producing a wrong merge — the caller (VideoEncodeJob) is expected to catch
/// this and fall back to independent per-preset dispatch.
/// </summary>
public class EncodingOrchestratorMergedDecomposeTests
{
    private const string GroupTag = "01HZMERGED0000000000000";

    private readonly Mock<IStrategyResolver> _resolver = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly Mock<IEncoder> _encoder = new();

    public EncodingOrchestratorMergedDecomposeTests()
    {
        _storage
            .Setup(expression: s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: (string path, CancellationToken _) => new(path: path));
        _storage.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());
    }

    private EncodingOrchestrator BuildOrchestrator() =>
        new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

    private static EncodingRequest BuildRequest(string presetName, Container container) =>
        new(
            InputPath: "/media/test.mkv",
            OutputDirectory: "out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: presetName,
                Container: container,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

    private static VideoOutputPlan MakeVideo(int width, int height, bool isHdrOutput) =>
        new(
            Width: width,
            Height: height,
            EncoderName: "libx265",
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: [],
            IsHdrOutput: isHdrOutput
        );

    private static OutputPlan MakePlan(
        OutputFormat format,
        int width,
        int height,
        bool isHdrOutput
    ) =>
        new(
            Format: format,
            VideoOutputs: [MakeVideo(width: width, height: height, isHdrOutput: isHdrOutput)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

    // ------------------------------------------------------------------
    // (c) single request merges to an identical plan / task set as before.
    // ------------------------------------------------------------------

    [Fact]
    public async Task DecomposeMergedAsync_SingleRequest_MatchesDecomposeAsync()
    {
        EncodingRequest request = BuildRequest(presetName: "Solo", container: Container.HlsTs);
        OutputPlan plan = MakePlan(format: OutputFormat.Hls, width: 1920, height: 1080, isHdrOutput: false);

        _encoder
            .Setup(expression: e => e.PlanAsync(It.IsAny<EncodingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: plan);

        Mock<IEncodingStrategy> strategy = new();
        strategy.Setup(expression: s => s.Format).Returns(value: OutputFormat.Hls);
        strategy.Setup(expression: s => s.EncodeMode).Returns(value: EncodeMode.SinglePass);
        strategy
            .Setup(expression: s => s.Decompose(It.IsAny<OutputPlan>(), It.IsAny<string>()))
            .Returns(
                valueFunction: (OutputPlan p, string tag) =>
                    [
                        new DecomposedTask(
                            TaskId: $"{tag}-video-0",
                            ParentJobId: 0,
                            GroupTag: tag,
                            Kind: EncodeTaskKind.Video,
                            OutputIndex: 0,
                            Resources: null,
                            Label: $"{p.VideoOutputs[0].Width}p"
                        ),
                    ]
            );
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = BuildOrchestrator();

        DecomposedTask[] viaDecomposeAsync = await orchestrator.DecomposeAsync(request: request, groupTag: GroupTag);
        DecomposedTask[] viaMerged = await orchestrator.DecomposeMergedAsync(requests: [request], groupTag: GroupTag);

        viaMerged.Should().BeEquivalentTo(expectation: viaDecomposeAsync);
    }

    // ------------------------------------------------------------------
    // Multi-preset merge wires ONE merged plan into ONE Decompose call.
    // ------------------------------------------------------------------

    [Fact]
    public async Task DecomposeMergedAsync_TwoPresets_CallsDecomposeOnceWithMergedPlan()
    {
        EncodingRequest fourKRequest = BuildRequest(presetName: "4K HDR HEVC", container: Container.HlsTs);
        EncodingRequest sdrRequest = BuildRequest(presetName: "1080p SDR HEVC", container: Container.HlsTs);

        OutputPlan fourKPlan = MakePlan(format: OutputFormat.Hls, width: 3840, height: 2160, isHdrOutput: true);
        OutputPlan sdrPlan = MakePlan(format: OutputFormat.Hls, width: 1920, height: 1080, isHdrOutput: false);

        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "4K HDR HEVC"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: fourKPlan);
        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "1080p SDR HEVC"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: sdrPlan);

        OutputPlan? capturedPlan = null;
        Mock<IEncodingStrategy> strategy = new();
        strategy.Setup(expression: s => s.Format).Returns(value: OutputFormat.Hls);
        strategy.Setup(expression: s => s.EncodeMode).Returns(value: EncodeMode.SinglePass);
        strategy
            .Setup(expression: s => s.Decompose(It.IsAny<OutputPlan>(), It.IsAny<string>()))
            .Returns(
                valueFunction: (OutputPlan p, string tag) =>
                {
                    capturedPlan = p;
                    return
                    [
                        new DecomposedTask(TaskId: $"{tag}-video-0", ParentJobId: 0, GroupTag: tag, Kind: EncodeTaskKind.Video, OutputIndex: 0, Resources: null),
                        new DecomposedTask(TaskId: $"{tag}-video-1", ParentJobId: 0, GroupTag: tag, Kind: EncodeTaskKind.Video, OutputIndex: 1, Resources: null),
                    ];
                }
            );
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = BuildOrchestrator();

        DecomposedTask[] tasks = await orchestrator.DecomposeMergedAsync(
            requests: [fourKRequest, sdrRequest],
            groupTag: GroupTag
        );

        strategy.Verify(
            expression: s => s.Decompose(It.IsAny<OutputPlan>(), GroupTag),
            times: Times.Once,
            failMessage: "one coordinated encode means exactly one Decompose call, not one per preset"
        );
        capturedPlan.Should().NotBeNull();
        capturedPlan!.VideoOutputs.Should().HaveCount(expected: 2);
        capturedPlan.VideoOutputs.Should().Contain(predicate: v => v.Width == 3840 && v.IsHdrOutput);
        capturedPlan.VideoOutputs.Should().Contain(predicate: v => v.Width == 1920 && !v.IsHdrOutput);
        tasks.Should().HaveCount(expected: 2);
    }

    // ------------------------------------------------------------------
    // Fallback contract: incompatible presets never silently produce a
    // wrong merge — they throw so the caller can fall back per-preset.
    // ------------------------------------------------------------------

    [Fact]
    public async Task DecomposeMergedAsync_DifferentContainers_Throws()
    {
        EncodingRequest hlsRequest = BuildRequest(presetName: "HLS preset", container: Container.HlsTs);
        EncodingRequest mkvRequest = BuildRequest(presetName: "MKV preset", container: Container.Mkv);

        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "HLS preset"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: MakePlan(format: OutputFormat.Hls, width: 1920, height: 1080, isHdrOutput: false));
        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "MKV preset"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: MakePlan(format: OutputFormat.Mkv, width: 1920, height: 1080, isHdrOutput: false));

        EncodingOrchestrator orchestrator = BuildOrchestrator();

        Func<Task> act = async () =>
            await orchestrator.DecomposeMergedAsync(requests: [hlsRequest, mkvRequest], groupTag: GroupTag);

        await act.Should().ThrowAsync<MergedEncodingIncompatibleException>();
    }

    [Fact]
    public async Task DecomposeMergedAsync_DifferentEncodeModes_Throws()
    {
        EncodingRequest singlePass = BuildRequest(presetName: "Single", container: Container.HlsTs) with
        {
            Profile = new(
                Id: Ulid.NewUlid(),
                Name: "Single",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.SinglePass
            ),
        };
        EncodingRequest twoPass = BuildRequest(presetName: "TwoPass", container: Container.HlsTs) with
        {
            Profile = new(
                Id: Ulid.NewUlid(),
                Name: "TwoPass",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.TwoPass
            ),
        };

        EncodingOrchestrator orchestrator = BuildOrchestrator();

        Func<Task> act = async () =>
            await orchestrator.DecomposeMergedAsync(requests: [singlePass, twoPass], groupTag: GroupTag);

        await act.Should().ThrowAsync<MergedEncodingIncompatibleException>();
    }

    [Fact]
    public async Task DecomposeMergedAsync_OnePresetFailsToPlan_Throws()
    {
        EncodingRequest good = BuildRequest(presetName: "Good", container: Container.HlsTs);
        EncodingRequest bad = BuildRequest(presetName: "Bad", container: Container.HlsTs);

        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "Good"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: MakePlan(format: OutputFormat.Hls, width: 1920, height: 1080, isHdrOutput: false));
        _encoder
            .Setup(expression: e =>
                e.PlanAsync(
                    It.Is<EncodingRequest>(r => r.Profile.Name == "Bad"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: (OutputPlan?)null);

        EncodingOrchestrator orchestrator = BuildOrchestrator();

        Func<Task> act = async () => await orchestrator.DecomposeMergedAsync(requests: [good, bad], groupTag: GroupTag);

        await act.Should().ThrowAsync<MergedEncodingIncompatibleException>();
    }

    [Fact]
    public async Task PlanMergedAsync_EmptyRequestList_ReturnsNull()
    {
        EncodingOrchestrator orchestrator = BuildOrchestrator();

        OutputPlan? result = await orchestrator.PlanMergedAsync(requests: []);

        result.Should().BeNull();
    }
}
