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
using NoMercy.Encoder.Hardware;

namespace NoMercy.Tests.Encoder.Codecs;

/// <summary>
/// AV1-specific resolver behavior. AV1 is the minefield of the codec
/// lineup because encoder families disagree on the fundamental shape of
/// the quality space:
///
///   libsvtav1 / libaom-av1 / av1_nvenc / av1_qsv : 0-51 (ish) / 0-63 / 0-51 / 1-51
///   av1_amf / av1_vaapi / librav1e              : 0-255 (!)
///
/// Set CRF 22 on a profile and the fixed "Default" quality is *relative*
/// to the encoder's range. Users who configure profiles in libsvtav1
/// terms (Crf=35 = balanced) will get near-lossless output if the
/// resolver picks av1_amf (QP 35 out of 255). These tests pin the
/// ranges so any future change to a definition breaks loudly.
///
/// Also pins the Apple omission: av1_videotoolbox does not exist.
/// Apple Silicon decodes AV1 in hardware but can't encode it, so the
/// codec definition must not declare one — otherwise the resolver
/// happily picks a phantom encoder and ffmpeg fails at runtime.
/// </summary>
public class Av1ArgumentResolverTests
{
    private static readonly CodecRegistry Registry = new();

    [Fact]
    public void LibSvtAv1_HasQualityRange_0To63()
    {
        EncoderInfo svt = Get(ffmpegName: "libsvtav1");
        svt.QualityRange.Min.Should().Be(expected: 0);
        svt.QualityRange.Max.Should().Be(expected: 63, because: "SVT-AV1 CRF caps at 63, not 51 like H264");
    }

    [Fact]
    public void LibSvtAv1_Supports10BitAndHdr()
    {
        EncoderInfo svt = Get(ffmpegName: "libsvtav1");
        svt.Supports10Bit.Should().BeTrue();
        svt.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void LibAomAv1_PresetsMapToCpuUsed_0To8()
    {
        // libaom-av1's "preset" is actually cpu-used (0=slowest, 8=fastest).
        // ResolvePreset must pass "6" through unmodified.
        EncoderInfo aom = Get(ffmpegName: "libaom-av1");
        aom.Presets.Should().BeEquivalentTo(expectation: ["0", "1", "2", "3", "4", "5", "6", "7", "8"]);
        EncoderArgumentResolver.ResolvePreset(profilePreset: "6", encoder: aom).Should().Be(expected: "6");
    }

    [Fact]
    public void LibRav1e_UsesQpRange_0To255_NotCrf()
    {
        // Rav1e's quality range is 0-255, and it only supports CQP + VBR —
        // NOT CRF. A profile configured with "Crf=22" (H264-style) would
        // silently map to QP 22/255 = near-lossless. The definition pins
        // the upper bound so any drift is caught.
        EncoderInfo rav1e = Get(ffmpegName: "librav1e");
        rav1e.QualityRange.Max.Should().Be(expected: 255);
        rav1e.SupportedRateControl.Should().NotContain(unexpected: RateControlMode.Crf);
        rav1e.SupportedRateControl.Should().Contain(expected: RateControlMode.Cqp);
    }

    [Fact]
    public void Av1Nvenc_MapsCrfToVbrCq_ScaledFromAv1Range()
    {
        // av1_nvenc uses 0-51 CQ, but the profile's CRF is written in the AV1
        // software reference scale (0-63). Without scaling, CRF 35 would land
        // as -cq 35 on a 0-51 scale = near-lossless. Expected: round(35/63*51)=28.
        ResolvedCodec nvenc = Resolve(ffmpegName: "av1_nvenc", vendor: GpuVendor.Nvidia, defaultRateControl: RateControlMode.Cq);
        Dictionary<string, string> flags = [];

        EncoderArgumentResolver.ResolveQuality(profileCrf: 35, resolved: nvenc, extraFlags: flags);

        flags[key: "-rc"].Should().Be(expected: "vbr");
        flags[key: "-cq"]
            .Should()
            .Be(expected: "28", because: "35/63 of the 0-51 range = 28; passing 35 raw would be near-lossless");
    }

    [Fact]
    public void Av1Amf_UsesFullQpRange_0To255()
    {
        // AMF AV1 QP is 0-255 even though H264/HEVC AMF use 0-51.
        // Critical to get right — CRF 35 passed naively becomes
        // -qp 35 (near-lossless) on av1_amf.
        EncoderInfo amf = Get(ffmpegName: "av1_amf");
        amf.QualityRange.Max.Should().Be(expected: 255, because: "AMD AV1 QP range is 0-255, NOT 0-51");
    }

    [Fact]
    public void Av1Amf_ScalesProfileCrfFromAv1RangeTo0To255()
    {
        // CRF 35 on the 0-63 AV1 software reference scale → QP ~142 on the
        // 0-255 AMF scale. Without scaling, av1_amf would receive -qp 35,
        // which in a 0-255 range is lossless territory (~10-20x the file
        // size the profile asked for).
        ResolvedCodec amf = Resolve(ffmpegName: "av1_amf", vendor: GpuVendor.Amd, defaultRateControl: RateControlMode.Cqp);
        Dictionary<string, string> flags = [];

        EncoderArgumentResolver.ResolveQuality(profileCrf: 35, resolved: amf, extraFlags: flags);

        flags[key: "-rc"].Should().Be(expected: "cqp");
        // round(35/63*255) = 142
        flags[key: "-qp"].Should().Be(expected: "142");
    }

    [Fact]
    public void ScaleQualityToEncoder_SameRange_ReturnsInput()
    {
        // No drift for the common case where encoder range matches reference.
        EncoderInfo libsvtav1 = Get(ffmpegName: "libsvtav1"); // 0-63 — matches AV1 ref
        EncoderArgumentResolver.ScaleQualityToEncoder(profileCrf: 35, encoder: libsvtav1).Should().Be(expected: 35);
    }

    [Fact]
    public void ScaleQualityToEncoder_ClampsToEncoderRange()
    {
        // If the profile somehow sends CRF 80 (above AV1 sw range), scaling
        // must still produce a value inside the target encoder's [Min, Max].
        EncoderInfo qsv = Get(ffmpegName: "av1_qsv"); // 1-51
        int scaled = EncoderArgumentResolver.ScaleQualityToEncoder(profileCrf: 80, encoder: qsv);
        scaled.Should().BeInRange(minimumValue: 1, maximumValue: 51);
    }

    [Fact]
    public void Av1Qsv_QualityStartsAt1_AndLacks10Bit()
    {
        EncoderInfo qsv = Get(ffmpegName: "av1_qsv");
        qsv.QualityRange.Min.Should().Be(expected: 1);
        qsv.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Av1Vaapi_HasNoPresets_FullQpRange()
    {
        EncoderInfo vaapi = Get(ffmpegName: "av1_vaapi");
        vaapi.Presets.Should().BeEmpty();
        vaapi.QualityRange.Max.Should().Be(expected: 255);
    }

    [Fact]
    public void Av1VideoToolbox_DoesNotExist()
    {
        // Apple Silicon decodes AV1 but can't encode. Resolver must never
        // find an av1_videotoolbox entry — otherwise ForceHardware on a
        // Mac would silently pick a phantom encoder.
        EncoderInfo[] av1Encoders = Registry
            .EnumerateVideoEncoders()
            .Where(predicate: t => t.CodecType == VideoCodecType.Av1)
            .Select(selector: t => t.Encoder)
            .ToArray();

        av1Encoders.Should().NotContain(predicate: e => e.FfmpegName == "av1_videotoolbox");
        av1Encoders.Should().NotContain(predicate: e => e.RequiredVendor == GpuVendor.Apple);
    }

    private static EncoderInfo Get(string ffmpegName)
    {
        foreach ((VideoCodecType c, EncoderInfo encoder) in Registry.EnumerateVideoEncoders())
        {
            if (c == VideoCodecType.Av1 && encoder.FfmpegName == ffmpegName)
                return encoder;
        }
        throw new InvalidOperationException(message: $"AV1 encoder {ffmpegName} not registered");
    }

    private static ResolvedCodec Resolve(
        string ffmpegName,
        GpuVendor? vendor,
        RateControlMode defaultRateControl
    )
    {
        EncoderInfo encoder = Get(ffmpegName: ffmpegName);
        GpuDevice? device = vendor is null
            ? null
            : new GpuDevice(
                Vendor: vendor.Value,
                Name: $"Test {vendor.Value}",
                VramMb: 16_384,
                MaxEncoderSessions: 12,
                SupportedCodecs: [VideoCodecType.Av1]
            );
        return new(FfmpegEncoderName: ffmpegName, EncoderInfo: encoder, Device: device, DefaultRateControl: defaultRateControl);
    }
}
