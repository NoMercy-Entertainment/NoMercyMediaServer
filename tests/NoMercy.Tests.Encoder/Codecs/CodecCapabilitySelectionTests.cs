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
/// Tests for codec selection based on hardware capabilities and encoder preferences.
/// Asserts that the CORRECT encoder is CHOSEN for a given hardware context and preference,
/// and that the resolved encoder metadata (rate control, device) is accurate.
/// </summary>
public class CodecCapabilitySelectionTests
{
    private readonly CodecRegistry _registry = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IHardwareCapabilities MakeCaps(GpuVendor? vendor, VideoCodecType[] codecs)
    {
        List<GpuDevice> gpus = [];
        if (vendor.HasValue)
            gpus.Add(item: new(Vendor: vendor.Value, Name: "Test GPU", VramMb: 8192, MaxEncoderSessions: 12, SupportedCodecs: codecs));
        return new HardwareCapabilities(Gpus: gpus, CpuCores: 8);
    }

    // ── Copy codec (stream remux, no encoding) ─────────────────────────────────

    [Fact]
    public void Copy_Returns_Sentinel_ResolvedCodec_With_Copy_Handle()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Copy, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "copy");
        resolved.Device.Should().BeNull();
        resolved.DefaultRateControl.Should().Be(expected: RateControlMode.Crf);
    }

    [Fact]
    public void Copy_Ignores_Preference_And_Hardware_State()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec result = resolver.Resolve(codec: VideoCodecType.Copy, hardware: noCaps);

        result.FfmpegEncoderName.Should().Be(expected: "copy");
    }

    // ── H.264 NVENC (NVIDIA hardware) ──────────────────────────────────────────

    [Fact]
    public void H264_PreferHardware_With_Nvidia_Gpu_SelectsNvenc()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.PreferHardware
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
        resolved.Device.Should().NotBeNull();
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Nvidia);
        resolved.DefaultRateControl.Should().Be(expected: RateControlMode.Cq);
    }

    [Fact]
    public void H264_ForceHardware_With_Nvidia_Gpu_SelectsNvenc()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceHardware
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Nvidia);
    }

    [Fact]
    public void H264_NvencDoesNotSupport10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.Supports10Bit.Should().BeFalse();
    }

    // ── H.265 / HEVC selection ─────────────────────────────────────────────────

    [Fact]
    public void H265_PreferHardware_With_Nvidia_Gpu_SelectsHevcNvenc()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_nvenc");
    }

    [Fact]
    public void H265_PreferHardware_With_Amd_Gpu_SelectsHevcAmf()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_amf");
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Amd);
    }

    [Fact]
    public void H265_PreferHardware_With_Intel_Gpu_SelectsHevcQsv()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_qsv");
    }

    [Fact]
    public void H265_PreferHardware_With_Apple_Gpu_SelectsHevcVideoToolbox()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Apple, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "hevc_videotoolbox");
    }

    // ── VP9 selection ──────────────────────────────────────────────────────────

    [Fact]
    public void Vp9_PreferHardware_With_Intel_SelectsVp9Qsv()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.Vp9]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Vp9, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "vp9_qsv");
    }

    [Fact]
    public void Vp9_PreferHardware_With_Nvidia_FallsBackToLibvpxVp9()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.Vp9]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Vp9, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "libvpx-vp9");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void Vp9_PreferHardware_With_Amd_FallsBackToLibvpxVp9()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.Vp9]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Vp9, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "libvpx-vp9");
    }

    // ── AV1 selection ──────────────────────────────────────────────────────────

    [Fact]
    public void Av1_PreferHardware_With_Nvidia_SelectsAv1Nvenc()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "av1_nvenc");
    }

    [Fact]
    public void Av1_PreferHardware_With_Intel_SelectsAv1Qsv()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "av1_qsv");
    }

    [Fact]
    public void Av1_PreferHardware_With_Amd_SelectsAv1Amf()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "av1_amf");
    }

    [Fact]
    public void Av1_PreferQuality_FallsBackToLibsvtav1()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.Av1,
            hardware: caps,
            preference: EncoderPreference.PreferQuality
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libsvtav1");
    }

    // ── Software encoders (ForceSoftware, PreferQuality) ───────────────────────

    [Fact]
    public void ForceSoftware_H264_SelectsLibx264_IgnoresNvidia()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceSoftware_H265_SelectsLibx265()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H265,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx265");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void PreferQuality_H264_SelectsLibx264_IgnoresAmd()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.PreferQuality
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
    }

    [Fact]
    public void PreferSpeed_With_Hardware_SelectsHardwareEncoder()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.PreferSpeed
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_qsv");
    }

    // ── No GPU availability ────────────────────────────────────────────────────

    [Fact]
    public void PreferHardware_NoGpu_FallsBackToSoftware()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: noCaps,
            preference: EncoderPreference.PreferHardware
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "libx264");
        resolved.Device.Should().BeNull();
    }

    [Fact]
    public void ForceHardware_NoGpu_Throws()
    {
        IHardwareCapabilities noCaps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        Action act = () =>
            resolver.Resolve(codec: VideoCodecType.H264, hardware: noCaps, preference: EncoderPreference.ForceHardware);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*hardware*");
    }

    // ── Codec support on GPU ───────────────────────────────────────────────────

    [Fact]
    public void PreferHardware_GpuLacksCodecSupport_FallsBackToSoftware()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "libx265");
    }

    // ── Rate control defaults per encoder ──────────────────────────────────────

    [Theory]
    [InlineData(data: [VideoCodecType.H264, "libx264", RateControlMode.Crf])]
    [InlineData(data: [VideoCodecType.H265, "libx265", RateControlMode.Crf])]
    [InlineData(data: [VideoCodecType.Av1, "libsvtav1", RateControlMode.Crf])]
    [InlineData(data: [VideoCodecType.Vp9, "libvpx-vp9", RateControlMode.Crf])]
    public void Software_Encoders_Default_To_Crf(
        VideoCodecType codec,
        string expectedHandle,
        RateControlMode expected
    )
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: codec, hardware: caps, preference: EncoderPreference.ForceSoftware);

        resolved.FfmpegEncoderName.Should().Be(expected: expectedHandle);
        resolved.DefaultRateControl.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: [VideoCodecType.H264, RateControlMode.Cq])]
    [InlineData(data: [VideoCodecType.H265, RateControlMode.Cq])]
    [InlineData(data: [VideoCodecType.Av1, RateControlMode.Cq])]
    public void Nvenc_Encoders_Default_To_Cq(VideoCodecType codec, RateControlMode expected)
    {
        IHardwareCapabilities caps = MakeCaps(
            vendor: GpuVendor.Nvidia,
            codecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
        );
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: codec, hardware: caps);

        resolved.DefaultRateControl.Should().Be(expected: expected);
    }

    [Fact]
    public void Intel_Qsv_Defaults_To_Icq()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.DefaultRateControl.Should().Be(expected: RateControlMode.Icq);
    }

    [Fact]
    public void Apple_VideoToolbox_Defaults_To_QualityLevel()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Apple, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.DefaultRateControl.Should().Be(expected: RateControlMode.QualityLevel);
    }

    // ── ResolveByEncoderName: explicit encoder name lookup ──────────────────────

    [Fact]
    public void ResolveByEncoderName_Matches_Case_Insensitive()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "H264_NVENC",
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void ResolveByEncoderName_InvalidHandle_FallsBackToPreference()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H264,
            ffmpegEncoderName: "unknown_encoder_xyz",
            hardware: caps
        );

        resolved.FfmpegEncoderName.Should().Be(expected: "h264_nvenc");
    }

    [Fact]
    public void ResolveByEncoderName_Finds_Gpu_Device_By_Vendor()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(
            codec: VideoCodecType.H265,
            ffmpegEncoderName: "hevc_amf",
            hardware: caps
        );

        resolved.Device.Should().NotBeNull();
        resolved.Device!.Vendor.Should().Be(expected: GpuVendor.Amd);
    }

    [Fact]
    public void ResolveByEncoderName_For_Copy_Returns_Copy()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.ResolveByEncoderName(codec: VideoCodecType.Copy, ffmpegEncoderName: "copy", hardware: caps);

        resolved.FfmpegEncoderName.Should().Be(expected: "copy");
    }

    // ── Encoder properties accessible via ResolvedCodec ─────────────────────────

    [Fact]
    public void LibsvtAv1_SupportsCrf_And_Cqp()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.Av1,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.SupportedRateControl.Should().Contain(expected: RateControlMode.Crf);
        resolved.EncoderInfo.SupportedRateControl.Should().Contain(expected: RateControlMode.Cqp);
    }

    [Fact]
    public void Libx264_Supports_Multiple_Presets()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.Presets.Should().Contain(expected: "fast");
        resolved.EncoderInfo.Presets.Should().Contain(expected: "medium");
        resolved.EncoderInfo.Presets.Should().Contain(expected: "slow");
    }

    [Fact]
    public void Libx264_Supports_Multiple_Profiles()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.Profiles.Should().Contain(expected: "baseline");
        resolved.EncoderInfo.Profiles.Should().Contain(expected: "main");
        resolved.EncoderInfo.Profiles.Should().Contain(expected: "high");
        resolved.EncoderInfo.Profiles.Should().Contain(expected: "high10");
    }

    [Fact]
    public void H264_Nvenc_HasMaxConcurrentSessions_Limit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.MaxConcurrentSessions.Should().Be(expected: 12);
    }

    [Fact]
    public void Software_Encoders_Have_Unlimited_Sessions()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.MaxConcurrentSessions.Should().Be(expected: int.MaxValue);
    }

    [Fact]
    public void Libx264_Supports10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Libx265_Supports10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H265,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void LibsvtAv1_Supports10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.Av1,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void H264_Nvenc_DoesNotSupport10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Av1_Amf_Does_Not_Support10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Amd, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.EncoderInfo.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Av1_Nvenc_Supports10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.EncoderInfo.Supports10Bit.Should().BeTrue();
    }

    [Fact]
    public void Av1_Qsv_Does_Not_Support10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.Av1]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.Av1, hardware: caps);

        resolved.EncoderInfo.Supports10Bit.Should().BeFalse();
    }

    [Fact]
    public void Software_Encoders_Support_Hdr()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H265,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.SupportsHdr.Should().BeTrue();
    }

    [Fact]
    public void Hardware_Encoders_May_Not_Support_Hdr()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.SupportsHdr.Should().BeFalse();
    }

    [Fact]
    public void Libx264_Has_Yuv420p10le_For_10Bit()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.PixelFormat10Bit.Should().Be(expected: "yuv420p10le");
    }

    [Fact]
    public void H264_Nvenc_Has_Empty_10Bit_Format()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Nvidia, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.PixelFormat10Bit.Should().Be(expected: "");
    }

    [Fact]
    public void Hevc_Videotoolbox_Has_Numeric_Profiles()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Apple, codecs: [VideoCodecType.H265]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H265, hardware: caps);

        resolved.EncoderInfo.Profiles.Should().Contain(expected: "1");
        resolved.EncoderInfo.Profiles.Should().Contain(expected: "2");
    }

    [Fact]
    public void H264_Qsv_QualityRange_Min_Is_One()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: GpuVendor.Intel, codecs: [VideoCodecType.H264]);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(codec: VideoCodecType.H264, hardware: caps);

        resolved.EncoderInfo.QualityRange.Min.Should().Be(expected: 1);
    }

    [Fact]
    public void Libx264_QualityRange_Min_Is_Zero()
    {
        IHardwareCapabilities caps = MakeCaps(vendor: null, codecs: []);
        CodecResolver resolver = new(registry: _registry);

        ResolvedCodec resolved = resolver.Resolve(
            codec: VideoCodecType.H264,
            hardware: caps,
            preference: EncoderPreference.ForceSoftware
        );

        resolved.EncoderInfo.QualityRange.Min.Should().Be(expected: 0);
    }
}
