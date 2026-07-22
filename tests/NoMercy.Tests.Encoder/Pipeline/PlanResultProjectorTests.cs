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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;

namespace NoMercy.Tests.Encoder.Pipeline;

public class PlanResultProjectorTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static ExecutionPlan BuildPlan(
        VideoOutputPlan[] videoOutputs,
        AudioOutputPlan[]? audioOutputs = null,
        SubtitleOutputPlan[]? subtitleOutputs = null,
        int segmentDuration = 6
    )
    {
        OutputPlan output = new(
            Format: OutputFormat.Hls,
            VideoOutputs: videoOutputs,
            AudioOutputs: audioOutputs ?? [],
            SubtitleOutputs: subtitleOutputs ?? [],
            Thumbnails: null,
            SegmentDurationSeconds: segmentDuration
        );

        return new(
            Groups: [],
            EstimatedTotalDuration: TimeSpan.FromMinutes(minutes: 10),
            OutputPlan: output
        );
    }

    private static VideoOutputPlan BuildVideoOutput(int crf, int bitrateKbps = 0) =>
        new(
            Width: 1920,
            Height: 1080,
            EncoderName: "libx264",
            Crf: crf,
            BitrateKbps: bitrateKbps,
            Preset: "medium",
            Profile: "high",
            Level: "4.1",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: new()
        );

    private static AudioOutputPlan BuildAudioOutput(string lang = "en") =>
        new(
            EncoderName: "aac",
            BitrateKbps: 192,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: lang,
            MapLabel: "0:a:0"
        );

    // ------------------------------------------------------------------
    // One-variant fixture: counts, strategy, keyframe, segment
    // ------------------------------------------------------------------

    [Fact]
    public void SingleVariant_CorrectVariantCount()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        result.Variants.Should().HaveCount(expected: 1);
    }

    [Fact]
    public void SingleVariant_StrategyContainsFormatAndCrf()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        result.Strategy.Should().Contain(expected: "Hls");
        result.Strategy.Should().Contain(expected: "CRF 23");
    }

    [Fact]
    public void SingleVariant_SegmentDurationPropagated()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23)], segmentDuration: 4);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        result.Variants[index: 0].SegmentDurationSeconds.Should().Be(expected: 4);
    }

    [Fact]
    public void SingleVariant_KeyframeIntervalPropagated()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23)], segmentDuration: 4);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        // KeyframeIntervalSeconds is derived from SegmentDurationSeconds until Phase 3
        result.Variants[index: 0].KeyframeIntervalSeconds.Should().BeGreaterThan(expected: 0);
    }

    // ------------------------------------------------------------------
    // RateControl — CRF only → RateControl.Crf
    // ------------------------------------------------------------------

    [Fact]
    public void CrfOnlyOutput_ProducesCrfRateControl()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23, bitrateKbps: 0)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        RateControl rateControl = result.Variants[index: 0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.Crf>();
        ((RateControl.Crf)rateControl).Value.Should().Be(expected: 23);
    }

    // ------------------------------------------------------------------
    // RateControl — bitrate only → RateControl.Abr
    // ------------------------------------------------------------------

    [Fact]
    public void BitrateOnlyOutput_ProducesAbrRateControl()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 0, bitrateKbps: 4000)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        RateControl rateControl = result.Variants[index: 0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.Abr>();
        ((RateControl.Abr)rateControl).BitrateKbps.Should().Be(expected: 4000);
    }

    // ------------------------------------------------------------------
    // RateControl — both CRF + bitrate → RateControl.CrfCapped
    // ------------------------------------------------------------------

    [Fact]
    public void CrfAndBitrateOutput_ProducesCrfCappedRateControl()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23, bitrateKbps: 4000)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);

        RateControl rateControl = result.Variants[index: 0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.CrfCapped>();
        RateControl.CrfCapped capped = (RateControl.CrfCapped)rateControl;
        capped.CrfValue.Should().Be(expected: 23);
        capped.MaxKbps.Should().Be(expected: 4000);
    }

    // ------------------------------------------------------------------
    // AsPlanResult extension — success path returns PlanResult
    // ------------------------------------------------------------------

    [Fact]
    public void AsPlanResult_OnSuccess_ReturnsPlanResult()
    {
        ExecutionPlan plan = BuildPlan(videoOutputs: [BuildVideoOutput(crf: 23)]);
        StageResult result = new StageSuccess<ExecutionPlan>(Value: plan);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult? planResult = result.AsPlanResult(context: ctx, projector: new PlanResultProjector());

        planResult.Should().NotBeNull();
        planResult!.Variants.Should().HaveCount(expected: 1);
    }

    [Fact]
    public void AsPlanResult_OnFailure_ReturnsNull()
    {
        StageResult result = new StageFailure(
            Error: new(Kind: EncodingErrorKind.Unknown, Message: "boom", FfmpegStderr: null, StageName: "Plan", Recoverable: false)
        );
        EncodingContext ctx = EncodingContext.Create();

        PlanResult? planResult = result.AsPlanResult(context: ctx, projector: new PlanResultProjector());

        planResult.Should().BeNull();
    }

    // ------------------------------------------------------------------
    // Snapshot test — 2-variant fixture serialises to expected JSON shape
    // ------------------------------------------------------------------

    [Fact]
    public void TwoVariants_JsonShapeIsStable()
    {
        ExecutionPlan plan = BuildPlan(
            videoOutputs:
            [
                BuildVideoOutput(crf: 23, bitrateKbps: 0),
                new(
                    Width: 1280,
                    Height: 720,
                    EncoderName: "libx264",
                    Crf: 25,
                    BitrateKbps: 0,
                    Preset: "medium",
                    Profile: "high",
                    Level: "4.0",
                    TenBit: false,
                    PixelFormat: "yuv420p",
                    MapLabel: "[v1]",
                    ExtraFlags: new()
                ),
            ],
            audioOutputs: [BuildAudioOutput(), BuildAudioOutput(lang: "fr")]
        );
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = new PlanResultProjector().FromExecutionPlan(plan: plan, context: ctx);
        string json = JsonConvert.SerializeObject(value: result, formatting: Formatting.Indented);
        JObject obj = JObject.Parse(json: json);

        // Top-level keys present
        obj.Should().ContainKey(expected: "Strategy");
        obj.Should().ContainKey(expected: "Variants");
        obj.Should().ContainKey(expected: "Subtitles");
        obj.Should().ContainKey(expected: "HardwareBindings");
        obj.Should().ContainKey(expected: "DecisionsLog");

        // Two variants
        JArray variants = (JArray)obj[propertyName: "Variants"]!;
        variants.Should().HaveCount(expected: 2);

        // Each variant has VariantId, Video, Audio
        ((JObject)variants[index: 0])
            .Should()
            .ContainKey(expected: "VariantId");
        ((JObject)variants[index: 0]).Should().ContainKey(expected: "Video");
        ((JObject)variants[index: 0]).Should().ContainKey(expected: "Audio");

        // First variant audio has 2 tracks (all audio on each variant)
        JArray audio0 = (JArray)variants[index: 0][key: "Audio"]!;
        audio0.Should().HaveCount(expected: 2);

        // RateControl tag is present on Video
        JObject video0 = (JObject)variants[index: 0][key: "Video"]!;
        video0.Should().ContainKey(expected: "RateControl");
    }
}
