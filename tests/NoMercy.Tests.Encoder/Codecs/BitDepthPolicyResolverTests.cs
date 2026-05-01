using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Codecs;

public class BitDepthPolicyResolverTests
{
    private readonly BitDepthPolicyResolver _resolver = new();

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    private static ResolvedCodec BuildCodec(
        string ffmpegName,
        bool supports10Bit,
        string pixelFormat10Bit = "yuv420p10le"
    ) =>
        new(
            FfmpegEncoderName: ffmpegName,
            EncoderInfo: new(
                FfmpegName: ffmpegName,
                RequiredVendor: null,
                Presets: ["medium"],
                Profiles: ["high"],
                Levels: ["4.1"],
                QualityRange: new(0, 51, 23),
                SupportedRateControl: [RateControlMode.Crf],
                Supports10Bit: supports10Bit,
                SupportsHdr: supports10Bit,
                MaxConcurrentSessions: int.MaxValue,
                PixelFormat10Bit: pixelFormat10Bit,
                VendorSpecificFlags: new()
            ),
            Device: null,
            DefaultRateControl: RateControlMode.Crf
        );

    private static ResolvedCodec H264NvencNo10Bit() =>
        BuildCodec("h264_nvenc", supports10Bit: false);

    private static ResolvedCodec H264QsvNo10Bit() => BuildCodec("h264_qsv", supports10Bit: false);

    private static ResolvedCodec HevcVideoToolboxNo10Bit() =>
        BuildCodec("hevc_videotoolbox", supports10Bit: false);

    private static ResolvedCodec Libx264Sw() =>
        BuildCodec("libx264", supports10Bit: false, pixelFormat10Bit: "yuv420p10le");

    private static ResolvedCodec Libx265Sw() =>
        BuildCodec("libx265", supports10Bit: true, pixelFormat10Bit: "yuv420p10le");

    private static IDecisionLogSink MakeSink() => new ScopedDecisionLog();

    private static Func<VideoCodecType, ResolvedCodec> SwReResolver(ResolvedCodec result) =>
        _ => result;

    private static Func<VideoCodecType, ResolvedCodec> NoopReResolver() =>
        _ => throw new InvalidOperationException("softwareReResolver should not be called");

    // -----------------------------------------------------------------------
    // 1. 8-bit request → always 8-bit regardless of policy
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(BitDepthPolicy.WarnAndDowngrade)]
    [InlineData(BitDepthPolicy.Strict)]
    [InlineData(BitDepthPolicy.PreferSoftware)]
    [InlineData(BitDepthPolicy.SilentDowngrade)]
    public void EightBit_request_returns_8bit_yuv420p_regardless_of_policy(BitDepthPolicy policy)
    {
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 8,
            policy: policy,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.FinalBitDepth.Should().Be(8);
        result.PixelFormat.Should().Be("yuv420p");
        result.Warning.Should().BeNull();
        result.Failure.Should().BeNull();
        result.SwitchedToEncoder.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // 2. 10-bit kept when encoder supports it
    // -----------------------------------------------------------------------

    [Fact]
    public void TenBit_kept_when_encoder_supports_it()
    {
        ResolvedCodec codec = Libx265Sw(); // Supports10Bit = true
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.WarnAndDowngrade,
            codec: VideoCodecType.H265,
            resolvedCodec: codec,
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.FinalBitDepth.Should().Be(10);
        result.PixelFormat.Should().Be("yuv420p10le");
        result.Warning.Should().BeNull();
        result.Failure.Should().BeNull();
        result.SwitchedToEncoder.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // 3. WarnAndDowngrade → 8-bit + warning rule, no failure
    // -----------------------------------------------------------------------

    [Fact]
    public void WarnAndDowngrade_returns_8bit_with_warning_rule_when_encoder_lacks_10bit()
    {
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.WarnAndDowngrade,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.FinalBitDepth.Should().Be(8);
        result.PixelFormat.Should().Be("yuv420p");
        result.Failure.Should().BeNull();
        result.Warning.Should().NotBeNull();
        result.Warning!.Id.Should().Be(EncoderRuleId.BitDepthAutoDowngrade);
        result.Warning.Severity.Should().Be(EncoderRuleSeverity.Warning);
    }

    // -----------------------------------------------------------------------
    // 4. Strict → failure with 422, no warning
    // -----------------------------------------------------------------------

    [Fact]
    public void Strict_returns_failure_with_422_when_encoder_lacks_10bit()
    {
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.Strict,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.Failure.Should().NotBeNull();
        result.Failure!.HttpStatusCode.Should().Be(422);
        result.Failure.Shape.Id.Should().Be(EncoderRuleId.BitDepthStrictViolation);
        result.Warning.Should().BeNull();
        result.FinalBitDepth.Should().Be(0);
    }

    // -----------------------------------------------------------------------
    // 5. PreferSoftware → switches to SW handle, keeps 10-bit
    // -----------------------------------------------------------------------

    [Fact]
    public void PreferSoftware_switches_to_SW_handle_keeps_10bit()
    {
        IDecisionLogSink sink = MakeSink();
        ResolvedCodec swCodec = BuildCodec("libx264", supports10Bit: true);

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.PreferSoftware,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: SwReResolver(swCodec)
        );

        result.FinalBitDepth.Should().Be(10);
        result.SwitchedToEncoder.Should().Be("libx264");
        result.PixelFormat.Should().Be("yuv420p10le");
        result.Warning.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // 6. SilentDowngrade → 8-bit, no warning rule, decision still emitted
    // -----------------------------------------------------------------------

    [Fact]
    public void SilentDowngrade_returns_8bit_with_no_warning_rule_but_emits_decision()
    {
        ScopedDecisionLog sink = new();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.SilentDowngrade,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.FinalBitDepth.Should().Be(8);
        result.PixelFormat.Should().Be("yuv420p");
        result.Warning.Should().BeNull();
        result.Failure.Should().BeNull();

        sink.Snapshot()
            .Should()
            .Contain(d =>
                d.Key == "plan.bit_depth"
                && d.Message.Contains("SilentDowngrade", StringComparison.OrdinalIgnoreCase)
            );
    }

    // -----------------------------------------------------------------------
    // 7. Decision log emitted for each branch (table-driven)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(8, BitDepthPolicy.WarnAndDowngrade, false, "plan.bit_depth")]
    [InlineData(10, BitDepthPolicy.WarnAndDowngrade, false, "plan.bit_depth")]
    [InlineData(10, BitDepthPolicy.WarnAndDowngrade, true, "plan.bit_depth")]
    [InlineData(10, BitDepthPolicy.Strict, false, "plan.bit_depth")]
    [InlineData(10, BitDepthPolicy.SilentDowngrade, false, "plan.bit_depth")]
    public void Decision_log_emitted_for_each_branch(
        int requestedBitDepth,
        BitDepthPolicy policy,
        bool encoderSupports10Bit,
        string expectedKey
    )
    {
        ScopedDecisionLog sink = new();
        ResolvedCodec codec = BuildCodec("h264_nvenc", encoderSupports10Bit);

        _resolver.Resolve(
            requestedBitDepth: requestedBitDepth,
            policy: policy,
            codec: VideoCodecType.H264,
            resolvedCodec: codec,
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        sink.Snapshot().Should().Contain(d => d.Key == expectedKey);
    }

    [Fact]
    public void Decision_log_emitted_for_PreferSoftware_branch()
    {
        ScopedDecisionLog sink = new();
        ResolvedCodec swCodec = BuildCodec("libx264", supports10Bit: true);

        _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.PreferSoftware,
            codec: VideoCodecType.H264,
            resolvedCodec: H264NvencNo10Bit(),
            decisions: sink,
            softwareReResolver: SwReResolver(swCodec)
        );

        sink.Snapshot().Should().Contain(d => d.Key == "plan.bit_depth_switched_to_software");
    }

    // -----------------------------------------------------------------------
    // 8. Per-encoder: policy fires same regardless of HW encoder identity
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_videotoolbox")]
    public void WarnAndDowngrade_fires_same_for_any_hw_encoder(string encoderName)
    {
        ResolvedCodec codec = BuildCodec(encoderName, supports10Bit: false);
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.WarnAndDowngrade,
            codec: VideoCodecType.H264,
            resolvedCodec: codec,
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.FinalBitDepth.Should().Be(8);
        result.Warning!.Id.Should().Be(EncoderRuleId.BitDepthAutoDowngrade);
        // The message contains the specific encoder name but the policy result is identical.
        result.Warning.Message.Should().Contain(encoderName);
    }

    [Theory]
    [InlineData("h264_nvenc")]
    [InlineData("h264_qsv")]
    [InlineData("hevc_videotoolbox")]
    public void Strict_fires_same_for_any_hw_encoder(string encoderName)
    {
        ResolvedCodec codec = BuildCodec(encoderName, supports10Bit: false);
        IDecisionLogSink sink = MakeSink();

        BitDepthResolutionResult result = _resolver.Resolve(
            requestedBitDepth: 10,
            policy: BitDepthPolicy.Strict,
            codec: VideoCodecType.H264,
            resolvedCodec: codec,
            decisions: sink,
            softwareReResolver: NoopReResolver()
        );

        result.Failure!.HttpStatusCode.Should().Be(422);
        result.Failure.Shape.Id.Should().Be(EncoderRuleId.BitDepthStrictViolation);
    }
}
