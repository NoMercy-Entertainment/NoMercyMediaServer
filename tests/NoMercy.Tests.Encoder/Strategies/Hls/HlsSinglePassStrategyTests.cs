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
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        Assert.Equal(OutputFormat.Hls, strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

        Assert.Equal(EncodeMode.SinglePass, strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_DelegatesToInjectedEncoder()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null,
                    Metrics: new(1024, 2.0, 24.0, "libx264", null)
                )
            );

        HlsSinglePassStrategy strategy = new(
            encoder.Object,
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
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
            request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.NotNull(result.Metrics);
        Assert.Equal("libx264", result.Metrics.EncoderUsed);
        encoder.Verify(
            e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // ── EstimateVideoCost cost-banding through Decompose ───────────────────

    [Theory]
    [InlineData(3840, 8)] // 4K → 8 units
    [InlineData(1920, 4)] // 1080p → 4 units
    [InlineData(1280, 2)] // 720p → 2 units
    [InlineData(854, 1)] // 480p → 1 unit
    [InlineData(640, 1)] // 360p → 1 unit (default)
    public void Decompose_VideoCostBanding_MatchesResolution(int width, int expectedCost)
    {
        // Cost units gate dispatcher concurrency — wrong banding = wrong
        // bundle sizing under load. Pin the mapping.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: width, height: width * 9 / 16)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, "g");

        DecomposedTask video = tasks.Single(t => t.Kind == EncodeTaskKind.Video);
        video.EstimatedCostUnits.Should().Be(expectedCost);
    }

    [Fact]
    public void Decompose_HdrToSdrConversion_AddsExtraCostUnit()
    {
        // SDR tonemap pass piles extra CPU work on top of decode/encode —
        // cost bumps by one to reflect that.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, convertHdrToSdr: true)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Video);

        // 1080p baseline = 4 units; +1 for tonemap = 5.
        task.EstimatedCostUnits.Should().Be(5);
    }

    [Fact]
    public void Decompose_VideoTask_PopulatesWidthAndEncoder()
    {
        // VideoWidth + VideoEncoderName drive bundle-cap resolution at
        // dispatch — must NOT be lost in decomposition.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video(width: 1920, height: 1080, encoderName: "hevc_nvenc")],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask task = strategy
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Video);

        task.VideoWidth.Should().Be(1920);
        task.VideoEncoderName.Should().Be("hevc_nvenc");
    }

    [Fact]
    public void Decompose_AudioTask_LabelIncludesLanguageAndChannels()
    {
        // Dashboard sorts on label — pinning the format catches reflow bugs
        // where channels or language ordering drifts.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
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
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Audio);

        task.Label.Should().Be("eng eac3 6ch");
    }

    [Fact]
    public void Decompose_AudioTaskNoLanguage_LabelUsesUnd()
    {
        // No language → "und" (undetermined) per ISO 639-2.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
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
            .Decompose(plan, "g")
            .Single(t => t.Kind == EncodeTaskKind.Audio);

        task.Label.Should().StartWith("und ");
    }

    [Fact]
    public void Decompose_ThumbnailsPresent_AddsOneTask()
    {
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [Video()],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: new(160, 90, 10)
        );

        DecomposedTask[] thumbs = strategy
            .Decompose(plan, "g")
            .Where(t => t.Kind == EncodeTaskKind.Thumbnails)
            .ToArray();

        thumbs.Should().ContainSingle();
        thumbs[0].Label.Should().Contain("160x90");
    }

    [Fact]
    public void Decompose_NoOutputs_ReturnsWholeTask()
    {
        // Empty plan → fall back to a single "whole" task so the strategy
        // contract (always at least one task) holds.
        HlsSinglePassStrategy strategy = new(
            Mock.Of<IEncoder>(),
            NullLogger<HlsSinglePassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
        OutputPlan plan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        DecomposedTask[] tasks = strategy.Decompose(plan, "g");

        tasks.Should().ContainSingle();
        tasks[0].Kind.Should().Be(EncodeTaskKind.Whole);
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
