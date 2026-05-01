using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Codecs;

public sealed record BitDepthResolutionResult(
    int FinalBitDepth,
    string? PixelFormat,
    string? SwitchedToEncoder,
    EncoderRule? Warning,
    EncoderRuntimeException? Failure
);

public interface IBitDepthPolicyResolver
{
    BitDepthResolutionResult Resolve(
        int requestedBitDepth,
        BitDepthPolicy policy,
        VideoCodecType codec,
        ResolvedCodec resolvedCodec,
        IDecisionLogSink decisions,
        Func<VideoCodecType, ResolvedCodec> softwareReResolver
    );
}

public sealed class BitDepthPolicyResolver : IBitDepthPolicyResolver
{
    public BitDepthResolutionResult Resolve(
        int requestedBitDepth,
        BitDepthPolicy policy,
        VideoCodecType codec,
        ResolvedCodec resolvedCodec,
        IDecisionLogSink decisions,
        Func<VideoCodecType, ResolvedCodec> softwareReResolver
    )
    {
        // 8-bit request — policy never applies.
        if (requestedBitDepth <= 8)
        {
            decisions.Add(
                new DecisionLog(
                    "plan",
                    "plan.bit_depth",
                    "8-bit requested — no policy check needed",
                    new { requested = 8 }
                )
            );
            return new BitDepthResolutionResult(
                FinalBitDepth: 8,
                PixelFormat: "yuv420p",
                SwitchedToEncoder: null,
                Warning: null,
                Failure: null
            );
        }

        // 10-bit request and encoder supports it — keep as-is.
        if (resolvedCodec.EncoderInfo.Supports10Bit)
        {
            string pf = string.IsNullOrEmpty(resolvedCodec.EncoderInfo.PixelFormat10Bit)
                ? "yuv420p10le"
                : resolvedCodec.EncoderInfo.PixelFormat10Bit;

            decisions.Add(
                new DecisionLog(
                    "plan",
                    "plan.bit_depth",
                    "10-bit kept — encoder supports it",
                    new { handle = resolvedCodec.FfmpegEncoderName, pixel_format = pf }
                )
            );

            return new BitDepthResolutionResult(
                FinalBitDepth: 10,
                PixelFormat: pf,
                SwitchedToEncoder: null,
                Warning: null,
                Failure: null
            );
        }

        // 10-bit requested but encoder lacks support — branch on policy.
        string encoderHandle = resolvedCodec.FfmpegEncoderName;

        return policy switch
        {
            BitDepthPolicy.WarnAndDowngrade => HandleWarnAndDowngrade(encoderHandle, decisions),
            BitDepthPolicy.Strict => HandleStrict(encoderHandle, decisions),
            BitDepthPolicy.PreferSoftware => HandlePreferSoftware(
                encoderHandle,
                codec,
                decisions,
                softwareReResolver
            ),
            BitDepthPolicy.SilentDowngrade => HandleSilentDowngrade(decisions),
            _ => HandleWarnAndDowngrade(encoderHandle, decisions),
        };
    }

    private static BitDepthResolutionResult HandleWarnAndDowngrade(
        string encoderHandle,
        IDecisionLogSink decisions
    )
    {
        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.bit_depth",
                "10-bit auto-downgraded to 8-bit",
                new { handle = encoderHandle, policy = nameof(BitDepthPolicy.WarnAndDowngrade) }
            )
        );

        EncoderRule warning = new(
            Id: EncoderRuleId.BitDepthAutoDowngrade,
            Severity: EncoderRuleSeverity.Warning,
            Field: "video_outputs[…].bit_depth",
            Message: $"Encoder '{encoderHandle}' does not support 10-bit — output will be 8-bit.",
            Fix: "Set bit_depth_policy = PreferSoftware to swap to libx264/libx265 instead, or accept 8-bit."
        );

        return new BitDepthResolutionResult(
            FinalBitDepth: 8,
            PixelFormat: "yuv420p",
            SwitchedToEncoder: null,
            Warning: warning,
            Failure: null
        );
    }

    private static BitDepthResolutionResult HandleStrict(
        string encoderHandle,
        IDecisionLogSink decisions
    )
    {
        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.bit_depth",
                "Strict violation — plan failed",
                new { handle = encoderHandle, policy = nameof(BitDepthPolicy.Strict) }
            )
        );

        EncoderRuntimeException failure = new(
            new EncoderErrorShape(
                Id: EncoderRuleId.BitDepthStrictViolation,
                Message: $"Encoder '{encoderHandle}' does not support 10-bit and bit_depth_policy = Strict forbids downgrade.",
                Suggestion: "Switch the profile to PreferSoftware or remove the 10-bit requirement.",
                Details: new { handle = encoderHandle }
            ),
            422
        );

        return new BitDepthResolutionResult(
            FinalBitDepth: 0,
            PixelFormat: null,
            SwitchedToEncoder: null,
            Warning: null,
            Failure: failure
        );
    }

    private static BitDepthResolutionResult HandlePreferSoftware(
        string fromHandle,
        VideoCodecType codec,
        IDecisionLogSink decisions,
        Func<VideoCodecType, ResolvedCodec> softwareReResolver
    )
    {
        ResolvedCodec sw = softwareReResolver(codec);
        string toHandle = sw.FfmpegEncoderName;

        string pf = string.IsNullOrEmpty(sw.EncoderInfo.PixelFormat10Bit)
            ? "yuv420p10le"
            : sw.EncoderInfo.PixelFormat10Bit;

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.bit_depth_switched_to_software",
                $"10-bit needed — switched from {fromHandle} to {toHandle}",
                new
                {
                    from = fromHandle,
                    to = toHandle,
                    codec = codec.ToString(),
                }
            )
        );

        return new BitDepthResolutionResult(
            FinalBitDepth: 10,
            PixelFormat: pf,
            SwitchedToEncoder: toHandle,
            Warning: null,
            Failure: null
        );
    }

    private static BitDepthResolutionResult HandleSilentDowngrade(IDecisionLogSink decisions)
    {
        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.bit_depth",
                "10-bit silently downgraded to 8-bit (policy = SilentDowngrade)",
                new { policy = nameof(BitDepthPolicy.SilentDowngrade) }
            )
        );

        return new BitDepthResolutionResult(
            FinalBitDepth: 8,
            PixelFormat: "yuv420p",
            SwitchedToEncoder: null,
            Warning: null,
            Failure: null
        );
    }
}
