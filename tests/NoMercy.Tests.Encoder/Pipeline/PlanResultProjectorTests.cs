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

        return new ExecutionPlan(
            Groups: [],
            EstimatedTotalDuration: TimeSpan.FromMinutes(10),
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
            ExtraFlags: new Dictionary<string, string>()
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
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        result.Variants.Should().HaveCount(1);
    }

    [Fact]
    public void SingleVariant_StrategyContainsFormatAndCrf()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        result.Strategy.Should().Contain("Hls");
        result.Strategy.Should().Contain("CRF 23");
    }

    [Fact]
    public void SingleVariant_SegmentDurationPropagated()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23)], segmentDuration: 4);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        result.Variants[0].SegmentDurationSeconds.Should().Be(4);
    }

    [Fact]
    public void SingleVariant_KeyframeIntervalPropagated()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23)], segmentDuration: 4);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        // KeyframeIntervalSeconds is derived from SegmentDurationSeconds until Phase 3
        result.Variants[0].KeyframeIntervalSeconds.Should().BeGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // RateControl — CRF only → RateControl.Crf
    // ------------------------------------------------------------------

    [Fact]
    public void CrfOnlyOutput_ProducesCrfRateControl()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23, bitrateKbps: 0)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        RateControl rateControl = result.Variants[0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.Crf>();
        ((RateControl.Crf)rateControl).Value.Should().Be(23);
    }

    // ------------------------------------------------------------------
    // RateControl — bitrate only → RateControl.Abr
    // ------------------------------------------------------------------

    [Fact]
    public void BitrateOnlyOutput_ProducesAbrRateControl()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 0, bitrateKbps: 4000)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        RateControl rateControl = result.Variants[0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.Abr>();
        ((RateControl.Abr)rateControl).BitrateKbps.Should().Be(4000);
    }

    // ------------------------------------------------------------------
    // RateControl — both CRF + bitrate → RateControl.CrfCapped
    // ------------------------------------------------------------------

    [Fact]
    public void CrfAndBitrateOutput_ProducesCrfCappedRateControl()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23, bitrateKbps: 4000)]);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);

        RateControl rateControl = result.Variants[0].Video.RateControl;
        rateControl.Should().BeOfType<RateControl.CrfCapped>();
        RateControl.CrfCapped capped = (RateControl.CrfCapped)rateControl;
        capped.CrfValue.Should().Be(23);
        capped.MaxKbps.Should().Be(4000);
    }

    // ------------------------------------------------------------------
    // AsPlanResult extension — success path returns PlanResult
    // ------------------------------------------------------------------

    [Fact]
    public void AsPlanResult_OnSuccess_ReturnsPlanResult()
    {
        ExecutionPlan plan = BuildPlan([BuildVideoOutput(crf: 23)]);
        StageResult result = new StageSuccess<ExecutionPlan>(plan);
        EncodingContext ctx = EncodingContext.Create();

        PlanResult? planResult = result.AsPlanResult(ctx);

        planResult.Should().NotBeNull();
        planResult!.Variants.Should().HaveCount(1);
    }

    [Fact]
    public void AsPlanResult_OnFailure_ReturnsNull()
    {
        StageResult result = new StageFailure(
            new(EncodingErrorKind.Unknown, "boom", null, "Plan", false)
        );
        EncodingContext ctx = EncodingContext.Create();

        PlanResult? planResult = result.AsPlanResult(ctx);

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
                new VideoOutputPlan(
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
                    ExtraFlags: new Dictionary<string, string>()
                ),
            ],
            audioOutputs: [BuildAudioOutput(), BuildAudioOutput("fr")]
        );
        EncodingContext ctx = EncodingContext.Create();

        PlanResult result = PlanResultProjector.FromExecutionPlan(plan, ctx);
        string json = JsonConvert.SerializeObject(result, Formatting.Indented);
        JObject obj = JObject.Parse(json);

        // Top-level keys present
        obj.Should().ContainKey("Strategy");
        obj.Should().ContainKey("Variants");
        obj.Should().ContainKey("Subtitles");
        obj.Should().ContainKey("HardwareBindings");
        obj.Should().ContainKey("DecisionsLog");

        // Two variants
        JArray variants = (JArray)obj["Variants"]!;
        variants.Should().HaveCount(2);

        // Each variant has VariantId, Video, Audio
        ((JObject)variants[0])
            .Should()
            .ContainKey("VariantId");
        ((JObject)variants[0]).Should().ContainKey("Video");
        ((JObject)variants[0]).Should().ContainKey("Audio");

        // First variant audio has 2 tracks (all audio on each variant)
        JArray audio0 = (JArray)variants[0]["Audio"]!;
        audio0.Should().HaveCount(2);

        // RateControl tag is present on Video
        JObject video0 = (JObject)variants[0]["Video"]!;
        video0.Should().ContainKey("RateControl");
    }
}
