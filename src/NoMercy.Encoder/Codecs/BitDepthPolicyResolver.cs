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

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Codecs;

public sealed record BitDepthResolutionResult(
    int FinalBitDepth,
    string? PixelFormat,
    string? SwitchedToEncoder,
    EncoderRule[] Warnings,
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
                entry: new(
                    Stage: "plan",
                    Key: "plan.bit_depth",
                    Message: "8-bit requested — no policy check needed",
                    Data: new { requested = 8 }
                )
            );
            return new(
                FinalBitDepth: 8,
                PixelFormat: "yuv420p",
                SwitchedToEncoder: null,
                Warnings: [],
                Failure: null
            );
        }

        // 10-bit request and encoder supports it — keep as-is.
        if (resolvedCodec.EncoderInfo.Supports10Bit)
        {
            string pf = string.IsNullOrEmpty(value: resolvedCodec.EncoderInfo.PixelFormat10Bit)
                ? "yuv420p10le"
                : resolvedCodec.EncoderInfo.PixelFormat10Bit;

            decisions.Add(
                entry: new(
                    Stage: "plan",
                    Key: "plan.bit_depth",
                    Message: "10-bit kept — encoder supports it",
                    Data: new { handle = resolvedCodec.FfmpegEncoderName, pixel_format = pf }
                )
            );

            return new(
                FinalBitDepth: 10,
                PixelFormat: pf,
                SwitchedToEncoder: null,
                Warnings: [],
                Failure: null
            );
        }

        // 10-bit requested but encoder lacks support — branch on policy.
        string encoderHandle = resolvedCodec.FfmpegEncoderName;

        return policy switch
        {
            BitDepthPolicy.WarnAndDowngrade => HandleWarnAndDowngrade(encoderHandle: encoderHandle, decisions: decisions),
            BitDepthPolicy.Strict => HandleStrict(encoderHandle: encoderHandle, decisions: decisions),
            BitDepthPolicy.PreferSoftware => HandlePreferSoftware(
                fromHandle: encoderHandle,
                codec: codec,
                decisions: decisions,
                softwareReResolver: softwareReResolver
            ),
            BitDepthPolicy.SilentDowngrade => HandleSilentDowngrade(decisions: decisions),
            _ => HandleWarnAndDowngrade(encoderHandle: encoderHandle, decisions: decisions),
        };
    }

    private static BitDepthResolutionResult HandleWarnAndDowngrade(
        string encoderHandle,
        IDecisionLogSink decisions
    )
    {
        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.bit_depth",
                Message: "10-bit auto-downgraded to 8-bit",
                Data: new { handle = encoderHandle, policy = nameof(BitDepthPolicy.WarnAndDowngrade) }
            )
        );

        // BitDepthNoHardwareSupport: why the downgrade happened (encoder capability gap).
        // BitDepthAutoDowngrade: what was done in response (8-bit fallback applied).
        // Both surface so the dashboard can show the cause and the consequence.
        EncoderRule noHwSupport = new(
            Id: EncoderRuleId.BitDepthNoHardwareSupport,
            Severity: EncoderRuleSeverity.Warning,
            Field: "video_outputs[…].bit_depth",
            Message: $"Encoder '{encoderHandle}' does not support 10-bit output.",
            Fix: "Switch hardware_preference to prefer_software (or force_software) to use a software encoder that supports 10-bit, or change bit_depth_policy to PreferSoftware."
        );

        EncoderRule autoDowngrade = new(
            Id: EncoderRuleId.BitDepthAutoDowngrade,
            Severity: EncoderRuleSeverity.Warning,
            Field: "video_outputs[…].bit_depth",
            Message: $"Encoder '{encoderHandle}' does not support 10-bit — output will be 8-bit.",
            Fix: "Set bit_depth_policy = PreferSoftware to swap to libx264/libx265 instead, or accept 8-bit."
        );

        return new(
            FinalBitDepth: 8,
            PixelFormat: "yuv420p",
            SwitchedToEncoder: null,
            Warnings: [noHwSupport, autoDowngrade],
            Failure: null
        );
    }

    private static BitDepthResolutionResult HandleStrict(
        string encoderHandle,
        IDecisionLogSink decisions
    )
    {
        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.bit_depth",
                Message: "Strict violation — plan failed",
                Data: new { handle = encoderHandle, policy = nameof(BitDepthPolicy.Strict) }
            )
        );

        EncoderRuntimeException failure = new(
            shape: new(
                Id: EncoderRuleId.BitDepthStrictViolation,
                Message: $"Encoder '{encoderHandle}' does not support 10-bit and bit_depth_policy = Strict forbids downgrade.",
                Suggestion: "Switch the profile to PreferSoftware or remove the 10-bit requirement.",
                Details: new { handle = encoderHandle }
            ),
            httpStatusCode: 422
        );

        return new(
            FinalBitDepth: 0,
            PixelFormat: null,
            SwitchedToEncoder: null,
            Warnings: [],
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
        ResolvedCodec sw = softwareReResolver(arg: codec);
        string toHandle = sw.FfmpegEncoderName;

        string pf = string.IsNullOrEmpty(value: sw.EncoderInfo.PixelFormat10Bit)
            ? "yuv420p10le"
            : sw.EncoderInfo.PixelFormat10Bit;

        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.bit_depth_switched_to_software",
                Message: $"10-bit needed — switched from {fromHandle} to {toHandle}",
                Data: new
                {
                    from = fromHandle,
                    to = toHandle,
                    codec = codec.ToString(),
                }
            )
        );

        return new(
            FinalBitDepth: 10,
            PixelFormat: pf,
            SwitchedToEncoder: toHandle,
            Warnings: [],
            Failure: null
        );
    }

    private static BitDepthResolutionResult HandleSilentDowngrade(IDecisionLogSink decisions)
    {
        decisions.Add(
            entry: new(
                Stage: "plan",
                Key: "plan.bit_depth",
                Message: "10-bit silently downgraded to 8-bit (policy = SilentDowngrade)",
                Data: new { policy = nameof(BitDepthPolicy.SilentDowngrade) }
            )
        );

        return new(
            FinalBitDepth: 8,
            PixelFormat: "yuv420p",
            SwitchedToEncoder: null,
            Warnings: [],
            Failure: null
        );
    }
}
