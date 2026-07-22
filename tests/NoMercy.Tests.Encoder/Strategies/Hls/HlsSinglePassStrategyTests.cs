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
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies.Hls;

public class HlsSinglePassStrategyTests
{
    [Fact]
    public void Format_IsHls()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        Assert.Equal(expected: OutputFormat.Hls, actual: strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        Assert.Equal(expected: EncodeMode.SinglePass, actual: strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_DelegatesToInjectedEncoder()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: null,
                    Metrics: new(OutputSizeBytes: 1024, AverageSpeed: 2.0, AverageFps: 24.0, EncoderUsed: "libx264", GpuUsed: null)
                )
            );

        HlsSinglePassStrategy strategy = new(
            encoder: encoder.Object,
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

        EncodingRequest request = new(
            InputPath: "/media/test.mkv",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "HLS 1080p",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(condition: result.Success);
        Assert.NotNull(@object: result.Metrics);
        Assert.Equal(expected: "libx264", actual: result.Metrics.EncoderUsed);
        encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // ── EstimateVideoCost cost-banding through Decompose ───────────────────

    [Theory]
    [InlineData(data: [3840, 8])] // 4K → 8 units
    [InlineData(data: [1920, 4])] // 1080p → 4 units
    [InlineData(data: [1280, 2])] // 720p → 2 units
    [InlineData(data: [854, 1])] // 480p → 1 unit
    [InlineData(data: [640, 1])] // 360p → 1 unit (default)
    public void Decompose_VideoCostBanding_MatchesResolution(int width, int expectedCost)
    {
        // Cost units gate dispatcher concurrency — wrong banding = wrong
        // bundle sizing under load. Pin the mapping.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: width, height: width * 9 / 16)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: "g");

        DecomposedTask video = tasks.Single(predicate: t => t.Kind == EncodeTaskKind.Video);
        video.EstimatedCostUnits.Should().Be(expected: expectedCost);
    }

    [Fact]
    public void Decompose_HdrToSdrConversion_AddsExtraCostUnit()
    {
        // SDR tonemap pass piles extra CPU work on top of decode/encode —
        // cost bumps by one to reflect that.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, convertHdrToSdr: true)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan: plan, groupTag: "g")
            .Single(predicate: t => t.Kind == EncodeTaskKind.Video);

        // 1080p baseline = 4 units; +1 for tonemap = 5.
        task.EstimatedCostUnits.Should().Be(expected: 5);
    }

    [Fact]
    public void Decompose_VideoTask_PopulatesWidthAndEncoder()
    {
        // VideoWidth + VideoEncoderName drive bundle-cap resolution at
        // dispatch — must NOT be lost in decomposition.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, encoderName: "hevc_nvenc")],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan: plan, groupTag: "g")
            .Single(predicate: t => t.Kind == EncodeTaskKind.Video);

        task.VideoWidth.Should().Be(expected: 1920);
        task.VideoEncoderName.Should().Be(expected: "hevc_nvenc");
    }

    [Fact]
    public void Decompose_AudioTask_LabelIncludesLanguageAndChannels()
    {
        // Dashboard sorts on label — pinning the format catches reflow bugs
        // where channels or language ordering drifts.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "eac3",
                    BitrateKbps: 384,
                    Channels: 6,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: "eng",
                    MapLabel: "[a0]"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan: plan, groupTag: "g")
            .Single(predicate: t => t.Kind == EncodeTaskKind.Audio);

        task.Label.Should().Be(expected: "eng eac3 6ch");
    }

    [Fact]
    public void Decompose_AudioTaskNoLanguage_LabelUsesUnd()
    {
        // No language → "und" (undetermined) per ISO 639-2.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs:
            [
                new(
                    EncoderName: "aac",
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRate: 48000,
                    Action: StreamAction.Transcode,
                    Language: null,
                    MapLabel: "[a0]"
                ),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan: plan, groupTag: "g")
            .Single(predicate: t => t.Kind == EncodeTaskKind.Audio);

        task.Label.Should().StartWith(expected: "und ");
    }

    [Fact]
    public void Decompose_ThumbnailsPresent_AddsOneTask()
    {
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: new(Width: 160, Height: 90, IntervalSeconds: 10)
        );

        DecomposedTask[] thumbs = strategy
            .Decompose(plan: plan, groupTag: "g")
            .Where(predicate: t => t.Kind == EncodeTaskKind.Thumbnails)
            .ToArray();

        thumbs.Should().ContainSingle();
        thumbs[0].Label.Should().Contain(expected: "160x90");
    }

    [Fact]
    public void Decompose_NoOutputs_ReturnsWholeTask()
    {
        // Empty plan → fall back to a single "whole" task so the strategy
        // contract (always at least one task) holds.
        HlsSinglePassStrategy strategy = new(
            encoder: Mock.Of<IEncoder>(),
            logger: NullLogger<HlsSinglePassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = strategy.Decompose(plan: plan, groupTag: "g");

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be(expected: EncodeTaskKind.Whole);
    }

    private static VideoOutputPlan Video(
        int width = 1280,
        int height = 720,
        string encoderName = "libx264",
        bool convertHdrToSdr = false
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "high",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: []
        )
        {
            ConvertHdrToSdr = convertHdrToSdr,
        };
}
